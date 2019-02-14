using System;
using System.Collections.Generic;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public class ParallelQueryMasterProjectionProcessingPhase : EventSubscriptionBasedProjectionProcessingPhase,
		IHandle<EventReaderSubscriptionMessage.CommittedEventReceived>,
		ISpoolStreamWorkItemContainer {
		//TODO: make it configurable
		public const int _maxScheduledSizePerWorker = 10000;
		public const int _maxUnmeasuredTasksPerWorker = 30;

		private readonly IProjectionStateHandler _stateHandler;
		private readonly SpooledStreamReadingDispatcher _spoolProcessingResponseDispatcher;
		private readonly Dictionary<Guid, SpoolStreamProcessingWorkItem> _spoolProcessingWorkItems;

		private ParallelProcessingLoadBalancer _loadBalancer;

		private SlaveProjectionCommunicationChannels _slaves;

		public ParallelQueryMasterProjectionProcessingPhase(
			CoreProjection coreProjection,
			Guid projectionCorrelationId,
			IPublisher publisher,
			IPublisher inputQueue,
			ProjectionConfig projectionConfig,
			Action updateStatistics,
			IProjectionStateHandler stateHandler,
			PartitionStateCache partitionStateCache,
			string name,
			ILogger logger,
			CheckpointTag zeroCheckpointTag,
			ICoreProjectionCheckpointManager checkpointManager,
			ReaderSubscriptionDispatcher subscriptionDispatcher,
			IReaderStrategy readerStrategy,
			IResultWriter resultWriter,
			bool checkpointsEnabled,
			bool stopOnEof,
			SpooledStreamReadingDispatcher spoolProcessingResponseDispatcher,
			IEmittedStreamsTracker emittedStreamsTracker)
			: base(
				publisher,
				inputQueue,
				coreProjection,
				projectionCorrelationId,
				checkpointManager,
				projectionConfig,
				name,
				logger,
				zeroCheckpointTag,
				partitionStateCache,
				resultWriter,
				updateStatistics,
				subscriptionDispatcher,
				readerStrategy,
				checkpointsEnabled,
				stopOnEof,
				orderedPartitionProcessing: true,
				isBiState: false,
				emittedStreamsTracker: emittedStreamsTracker) {
			_stateHandler = stateHandler;
			_spoolProcessingResponseDispatcher = spoolProcessingResponseDispatcher;
			_spoolProcessingWorkItems = new Dictionary<Guid, SpoolStreamProcessingWorkItem>();
		}


		public override void NewCheckpointStarted(CheckpointTag at) {
		}

		public override void Dispose() {
		}

		public override void AssignSlaves(SlaveProjectionCommunicationChannels slaveProjections) {
			_slaves = slaveProjections;
			var workerCount = _slaves.Channels["slave"].Length;
			_loadBalancer = new ParallelProcessingLoadBalancer(
				workerCount, _maxScheduledSizePerWorker, _maxUnmeasuredTasksPerWorker);
		}

		public override void Subscribe(CheckpointTag @from, bool fromCheckpoint) {
			if (_slaves == null)
				throw new InvalidOperationException(
					"Cannot subscribe to event reader without assigned slave projections");
			base.Subscribe(@from, fromCheckpoint);
		}

		public void Handle(EventReaderSubscriptionMessage.CommittedEventReceived message) {
			//TODO:  make sure this is no longer required : if (_state != State.StateLoaded)
			if (IsOutOfOrderSubscriptionMessage(message))
				return;
			RegisterSubscriptionMessage(message);
			try {
				var eventTag = message.CheckpointTag;
				var correlationId = Guid.NewGuid();
				var committedEventWorkItem = new SpoolStreamProcessingWorkItem(
					this,
					_publisher,
					_resultWriter,
					_loadBalancer,
					message,
					_slaves,
					_spoolProcessingResponseDispatcher,
					_subscriptionStartedAtLastCommitPosition,
					_currentSubscriptionId,
					_stateHandler.GetSourceDefinition().DefinesCatalogTransform);
				_spoolProcessingWorkItems.Add(correlationId, committedEventWorkItem);
				_processingQueue.EnqueueTask(committedEventWorkItem, eventTag);
				if (_state == PhaseState.Running) // prevent processing mostly one projection
					EnsureTickPending();
			} catch (Exception ex) {
				_coreProjection.SetFaulted(ex);
			}
		}

		public string TransformCatalogEvent(CheckpointTag position, ResolvedEvent @event) {
			return _stateHandler.TransformCatalogEvent(position, @event);
		}

		public void CompleteSpoolProcessingWorkItem(Guid correlationId, CheckpointTag position) {
			_spoolProcessingWorkItems.Remove(correlationId);
			_checkpointManager.EventProcessed(position, 18.8f);
		}
	}
}
