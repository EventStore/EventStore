using System;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public abstract class EventReaderBasedProjectionProcessingStrategy : ProjectionProcessingStrategy {
		protected readonly ProjectionConfig _projectionConfig;
		protected readonly IQuerySources _sourceDefinition;
		private readonly ReaderSubscriptionDispatcher _subscriptionDispatcher;
		private readonly bool _isBiState;

		protected EventReaderBasedProjectionProcessingStrategy(
			string name, ProjectionVersion projectionVersion, ProjectionConfig projectionConfig,
			IQuerySources sourceDefinition, ILogger logger, ReaderSubscriptionDispatcher subscriptionDispatcher)
			: base(name, projectionVersion, logger) {
			_projectionConfig = projectionConfig;
			_sourceDefinition = sourceDefinition;
			_subscriptionDispatcher = subscriptionDispatcher;
			_isBiState = sourceDefinition.IsBiState;
		}

		public override sealed IProjectionProcessingPhase[] CreateProcessingPhases(
			IPublisher publisher,
			IPublisher inputQueue,
			Guid projectionCorrelationId,
			PartitionStateCache partitionStateCache,
			Action updateStatistics,
			CoreProjection coreProjection,
			ProjectionNamesBuilder namingBuilder,
			ITimeProvider timeProvider,
			IODispatcher ioDispatcher,
			CoreProjectionCheckpointWriter coreProjectionCheckpointWriter) {
			var definesFold = _sourceDefinition.DefinesFold;

			var readerStrategy = CreateReaderStrategy(timeProvider);

			var zeroCheckpointTag = readerStrategy.PositionTagger.MakeZeroCheckpointTag();

			var checkpointManager = CreateCheckpointManager(
				projectionCorrelationId,
				publisher,
				ioDispatcher,
				namingBuilder,
				coreProjectionCheckpointWriter,
				definesFold,
				readerStrategy);

			var resultWriter = CreateFirstPhaseResultWriter(
				checkpointManager as IEmittedEventWriter,
				zeroCheckpointTag,
				namingBuilder);

			var emittedStreamsTracker = new EmittedStreamsTracker(ioDispatcher, _projectionConfig, namingBuilder);

			var firstPhase = CreateFirstProcessingPhase(
				publisher,
				inputQueue,
				projectionCorrelationId,
				partitionStateCache,
				updateStatistics,
				coreProjection,
				_subscriptionDispatcher,
				zeroCheckpointTag,
				checkpointManager,
				readerStrategy,
				resultWriter,
				emittedStreamsTracker);

			return CreateProjectionProcessingPhases(
				publisher,
				inputQueue,
				projectionCorrelationId,
				namingBuilder,
				partitionStateCache,
				coreProjection,
				ioDispatcher,
				firstPhase);
		}

		protected abstract IProjectionProcessingPhase CreateFirstProcessingPhase(
			IPublisher publisher,
			IPublisher inputQueue,
			Guid projectionCorrelationId,
			PartitionStateCache partitionStateCache,
			Action updateStatistics,
			CoreProjection coreProjection,
			ReaderSubscriptionDispatcher subscriptionDispatcher,
			CheckpointTag zeroCheckpointTag,
			ICoreProjectionCheckpointManager checkpointManager,
			IReaderStrategy readerStrategy,
			IResultWriter resultWriter,
			IEmittedStreamsTracker emittedStreamsTracker);

		protected virtual IReaderStrategy CreateReaderStrategy(ITimeProvider timeProvider) {
			return ReaderStrategy.Create(
				_name,
				0,
				_sourceDefinition,
				timeProvider,
				_projectionConfig.StopOnEof,
				_projectionConfig.RunAs);
		}

		protected abstract IResultEventEmitter CreateFirstPhaseResultEmitter(ProjectionNamesBuilder namingBuilder);

		protected abstract IProjectionProcessingPhase[] CreateProjectionProcessingPhases(
			IPublisher publisher,
			IPublisher inputQueue,
			Guid projectionCorrelationId,
			ProjectionNamesBuilder namingBuilder,
			PartitionStateCache partitionStateCache,
			CoreProjection coreProjection,
			IODispatcher ioDispatcher,
			IProjectionProcessingPhase firstPhase);

		protected override IQuerySources GetSourceDefinition() {
			return _sourceDefinition;
		}

		public override bool GetRequiresRootPartition() {
			return !(_sourceDefinition.ByStreams || _sourceDefinition.ByCustomPartitions) || _isBiState;
		}

		public override void EnrichStatistics(ProjectionStatistics info) {
			//TODO: get rid of this cast
			info.ResultStreamName = _sourceDefinition.ResultStreamNameOption;
		}

		protected virtual ICoreProjectionCheckpointManager CreateCheckpointManager(
			Guid projectionCorrelationId, IPublisher publisher, IODispatcher ioDispatcher,
			ProjectionNamesBuilder namingBuilder, CoreProjectionCheckpointWriter coreProjectionCheckpointWriter,
			bool definesFold, IReaderStrategy readerStrategy) {
			var emitAny = _projectionConfig.EmitEventEnabled;

			//NOTE: not emitting one-time/transient projections are always handled by default checkpoint manager
			// as they don't depend on stable event order
			if (emitAny && !readerStrategy.IsReadingOrderRepeatable) {
				return new MultiStreamMultiOutputCheckpointManager(
					publisher, projectionCorrelationId, _projectionVersion, _projectionConfig.RunAs, ioDispatcher,
					_projectionConfig, _name, readerStrategy.PositionTagger, namingBuilder,
					_projectionConfig.CheckpointsEnabled, GetProducesRunningResults(), definesFold,
					coreProjectionCheckpointWriter);
			} else {
				return new DefaultCheckpointManager(
					publisher, projectionCorrelationId, _projectionVersion, _projectionConfig.RunAs, ioDispatcher,
					_projectionConfig, _name, readerStrategy.PositionTagger, namingBuilder,
					_projectionConfig.CheckpointsEnabled, GetProducesRunningResults(), definesFold,
					coreProjectionCheckpointWriter);
			}
		}

		protected virtual IResultWriter CreateFirstPhaseResultWriter(
			IEmittedEventWriter emittedEventWriter, CheckpointTag zeroCheckpointTag,
			ProjectionNamesBuilder namingBuilder) {
			return new ResultWriter(
				CreateFirstPhaseResultEmitter(namingBuilder), emittedEventWriter, GetProducesRunningResults(),
				zeroCheckpointTag, namingBuilder.GetPartitionCatalogStreamName());
		}
	}

	public abstract class DefaultProjectionProcessingStrategy : EventReaderBasedProjectionProcessingStrategy {
		private readonly IProjectionStateHandler _stateHandler;

		protected DefaultProjectionProcessingStrategy(
			string name, ProjectionVersion projectionVersion, IProjectionStateHandler stateHandler,
			ProjectionConfig projectionConfig, IQuerySources sourceDefinition, ILogger logger,
			ReaderSubscriptionDispatcher subscriptionDispatcher)
			: base(name, projectionVersion, projectionConfig, sourceDefinition, logger, subscriptionDispatcher) {
			_stateHandler = stateHandler;
		}

		protected override IProjectionProcessingPhase CreateFirstProcessingPhase(
			IPublisher publisher,
			IPublisher inputQueue,
			Guid projectionCorrelationId,
			PartitionStateCache partitionStateCache,
			Action updateStatistics,
			CoreProjection coreProjection,
			ReaderSubscriptionDispatcher subscriptionDispatcher,
			CheckpointTag zeroCheckpointTag,
			ICoreProjectionCheckpointManager checkpointManager,
			IReaderStrategy readerStrategy,
			IResultWriter resultWriter,
			IEmittedStreamsTracker emittedStreamsTracker) {
			var statePartitionSelector = CreateStatePartitionSelector();

			var orderedPartitionProcessing = _sourceDefinition.ByStreams && _sourceDefinition.IsBiState;
			return new EventProcessingProjectionProcessingPhase(
				coreProjection,
				projectionCorrelationId,
				publisher,
				inputQueue,
				_projectionConfig,
				updateStatistics,
				_stateHandler,
				partitionStateCache,
				_sourceDefinition.DefinesStateTransform,
				_name,
				_logger,
				zeroCheckpointTag,
				checkpointManager,
				statePartitionSelector,
				subscriptionDispatcher,
				readerStrategy,
				resultWriter,
				_projectionConfig.CheckpointsEnabled,
				this.GetStopOnEof(),
				_sourceDefinition.IsBiState,
				orderedPartitionProcessing: orderedPartitionProcessing,
				emittedStreamsTracker: emittedStreamsTracker);
		}

		protected virtual StatePartitionSelector CreateStatePartitionSelector() {
			return _sourceDefinition.ByCustomPartitions
				? new ByHandleStatePartitionSelector(_stateHandler)
				: (_sourceDefinition.ByStreams
					? (StatePartitionSelector)new ByStreamStatePartitionSelector()
					: new NoopStatePartitionSelector());
		}
	}
}
