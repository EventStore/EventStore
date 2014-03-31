using System;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class SlaveQueryProcessingStrategy : DefaultProjectionProcessingStrategy
    {
        private readonly Guid _workerId;
        private readonly IPublisher _resultsPublisher;
        private readonly Guid _masterCoreProjectionId;

        public SlaveQueryProcessingStrategy(
            string name, ProjectionVersion projectionVersion, IProjectionStateHandler stateHandler,
            ProjectionConfig projectionConfig, IQuerySources sourceDefinition, ILogger logger,
            Guid workerId, IPublisher resultsPublisher, Guid masterCoreProjectionId,
            ReaderSubscriptionDispatcher subscriptionDispatcher)
            : base(
                name, projectionVersion, stateHandler, projectionConfig, sourceDefinition, logger,
                subscriptionDispatcher)
        {
            _workerId = workerId;
            _resultsPublisher = resultsPublisher;
            _masterCoreProjectionId = masterCoreProjectionId;
        }

        public override bool GetStopOnEof()
        {
            return true;
        }

        public override bool GetUseCheckpoints()
        {
            return false;
        }

        public override bool GetProducesRunningResults()
        {
            return false;
        }

        public override bool GetIsSlaveProjection()
        {
            return true;
        }

        public override SlaveProjectionDefinitions GetSlaveProjections()
        {
            return null;
        }

        protected override IProjectionProcessingPhase[] CreateProjectionProcessingPhases(
            IPublisher publisher, Guid projectionCorrelationId, ProjectionNamesBuilder namingBuilder,
            PartitionStateCache partitionStateCache, CoreProjection coreProjection, IODispatcher ioDispatcher,
            IProjectionProcessingPhase firstPhase)
        {
            return new[] {firstPhase};
        }

        protected override IResultEventEmitter CreateFirstPhaseResultEmitter(ProjectionNamesBuilder namingBuilder)
        {
            throw new NotImplementedException();
        }

        protected override IReaderStrategy CreateReaderStrategy(ITimeProvider timeProvider)
        {
            return new ExternallyFedReaderStrategy(
                0, _projectionConfig.RunAs, timeProvider, _sourceDefinition.LimitingCommitPosition ?? long.MinValue);
        }

        protected override IResultWriter CreateFirstPhaseResultWriter(
            IEmittedEventWriter emittedEventWriter, CheckpointTag zeroCheckpointTag,
            ProjectionNamesBuilder namingBuilder)
        {
            return new SlaveResultWriter(_resultsPublisher, _workerId, _masterCoreProjectionId);
        }

        protected override ICoreProjectionCheckpointManager CreateCheckpointManager(
            Guid projectionCorrelationId, IPublisher publisher, IODispatcher ioDispatcher, ProjectionNamesBuilder namingBuilder,
            CoreProjectionCheckpointWriter coreProjectionCheckpointWriter, bool definesFold, IReaderStrategy readerStrategy)
        {
            return new NoopCheckpointManager(
                publisher, projectionCorrelationId, _projectionConfig, _name, readerStrategy.PositionTagger,
                namingBuilder);
        }

        protected override StatePartitionSelector CreateStatePartitionSelector()
        {
            return new ByPositionStreamStatePartitionSelector();
        }
    }
}
