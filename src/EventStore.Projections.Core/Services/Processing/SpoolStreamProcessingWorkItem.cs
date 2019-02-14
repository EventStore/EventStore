using System;
using EventStore.Core.Bus;
using EventStore.Core.Services;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;

namespace EventStore.Projections.Core.Services.Processing {
	public class SpoolStreamProcessingWorkItem : WorkItem,
		IHandle<PartitionProcessingResult>,
		IHandle<PartitionMeasured>,
		IHandle<PartitionProcessingProgress> {
		private readonly ISpoolStreamWorkItemContainer _container;
		private readonly IResultWriter _resultWriter;
		private readonly ParallelProcessingLoadBalancer _loadBalancer;
		private readonly EventReaderSubscriptionMessage.CommittedEventReceived _message;

		private readonly SpooledStreamReadingDispatcher _spoolProcessingResponseDispatcher;

		private readonly SlaveProjectionCommunicationChannels _slaves;
		private PartitionProcessingResult _resultMessage;
		private Tuple<Guid, string> _spoolRequestId;
		private readonly long _limitingCommitPosition;
		private readonly Guid _subscriptionId;
		private readonly bool _definesCatalogTransform;
		private CheckpointTag _completedAtPosition;
		private readonly IPublisher _publisher;

		public SpoolStreamProcessingWorkItem(
			ISpoolStreamWorkItemContainer container,
			IPublisher publisher,
			IResultWriter resultWriter,
			ParallelProcessingLoadBalancer loadBalancer,
			EventReaderSubscriptionMessage.CommittedEventReceived message,
			SlaveProjectionCommunicationChannels slaves,
			SpooledStreamReadingDispatcher spoolProcessingResponseDispatcher,
			long limitingCommitPosition,
			Guid subscriptionId,
			bool definesCatalogTransform)
			: base(Guid.NewGuid()) {
			if (publisher == null) throw new ArgumentNullException("publisher");
			if (resultWriter == null) throw new ArgumentNullException("resultWriter");
			if (slaves == null) throw new ArgumentNullException("slaves");
			if (spoolProcessingResponseDispatcher == null)
				throw new ArgumentNullException("spoolProcessingResponseDispatcher");
			_container = container;
			_publisher = publisher;
			_resultWriter = resultWriter;
			_loadBalancer = loadBalancer;
			_message = message;
			_slaves = slaves;
			_spoolProcessingResponseDispatcher = spoolProcessingResponseDispatcher;
			_limitingCommitPosition = limitingCommitPosition;
			_subscriptionId = subscriptionId;
			_definesCatalogTransform = definesCatalogTransform;
		}

		protected override void ProcessEvent() {
			var channelGroup = _slaves.Channels["slave"];
			var resolvedEvent = _message.Data;
			var position = _message.CheckpointTag;

			var streamId = TransformCatalogEvent(position, resolvedEvent);
			if (string.IsNullOrEmpty(streamId))
				CompleteProcessing(position);
			else
				_loadBalancer.ScheduleTask(
					streamId,
					(streamId_, workerIndex) => {
						var channel = channelGroup[workerIndex];
						_spoolRequestId = _spoolProcessingResponseDispatcher.PublishSubscribe(
							_publisher,
							new ReaderSubscriptionManagement.SpoolStreamReading(
								channel.WorkerId,
								channel.SubscriptionId,
								streamId_,
								resolvedEvent.PositionSequenceNumber,
								_limitingCommitPosition),
							this);
					});
		}

		private void CompleteProcessing(CheckpointTag completedAtPosition) {
			_completedAtPosition = completedAtPosition;
			NextStage();
		}

		private string TransformCatalogEvent(CheckpointTag position, ResolvedEvent resolvedEvent) {
			if (_definesCatalogTransform)
				return _container.TransformCatalogEvent(position, resolvedEvent);
			return SystemEventTypes.StreamReferenceEventToStreamId(resolvedEvent.EventType, resolvedEvent.Data);
		}

		protected override void WriteOutput() {
			if (_resultMessage != null) {
				_resultWriter.WriteEofResult(
					_subscriptionId, _resultMessage.Partition, _resultMessage.Result, _resultMessage.Position,
					Guid.Empty, null);
			}

			_container.CompleteSpoolProcessingWorkItem(_subscriptionId, _completedAtPosition);
			NextStage();
		}

		public void Handle(PartitionProcessingResult message) {
			_loadBalancer.AccountCompleted(message.Partition);
			_spoolProcessingResponseDispatcher.Cancel(_spoolRequestId);
			_resultMessage = message;
			CompleteProcessing(message.Position);
		}

		public void Handle(PartitionMeasured message) {
			_loadBalancer.AccountMeasured(message.Partition, message.Size);
		}

		public void Handle(PartitionProcessingProgress message) {
			throw new NotImplementedException();
		}
	}

	public interface ISpoolStreamWorkItemContainer {
		string TransformCatalogEvent(CheckpointTag position, ResolvedEvent @event);
		void CompleteSpoolProcessingWorkItem(Guid subscriptionId, CheckpointTag position);
	}
}
