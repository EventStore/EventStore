using System;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ProjectionProcessingStrategy
    {
        private readonly string _name;
        private readonly ProjectionVersion _projectionVersion;
        private readonly IProjectionStateHandler _stateHandler;
        private readonly ProjectionConfig _projectionConfig;
        private readonly IQuerySources _sourceDefinition;
        private readonly ILogger _logger;

        public ProjectionProcessingStrategy(
            string name, ProjectionVersion projectionVersion, IProjectionStateHandler stateHandler,
            ProjectionConfig projectionConfig, IQuerySources sourceDefinition, ILogger logger)
        {
            _name = name;
            _projectionVersion = projectionVersion;
            _stateHandler = stateHandler;
            _projectionConfig = projectionConfig;
            _sourceDefinition = sourceDefinition;
            _logger = logger;
        }

        private CoreProjection InternalCreate(
            Guid projectionCorrelationId, IPublisher publisher, IODispatcher ioDispatcher,
            PublishSubscribeDispatcher<ReaderSubscriptionManagement.Subscribe, ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage> subscriptionDispatcher, ITimeProvider timeProvider,
            out ProjectionSourceDefinition preparedSourceDefinition)
        {
            var s = new SourceDefinition(_sourceDefinition);
            var builder = new CheckpointStrategy.Builder();
            s.ConfigureSourceProcessingStrategy(builder);
            var sourceDefinitionRecorder = new SourceDefinitionRecorder();
            s.ConfigureSourceProcessingStrategy(sourceDefinitionRecorder);
            preparedSourceDefinition = sourceDefinitionRecorder.Build(_name);


            var namingBuilder = new ProjectionNamesBuilder(_name, preparedSourceDefinition.Options);

            
            return new CoreProjection(_projectionVersion, projectionCorrelationId, publisher, _stateHandler,
                _projectionConfig, ioDispatcher, subscriptionDispatcher, _logger, namingBuilder,
                this, timeProvider);
        }

        public CoreProjection CreateAndPrepare(
            Guid projectionCorrelationId, IPublisher publisher, IODispatcher ioDispatcher,
            PublishSubscribeDispatcher<ReaderSubscriptionManagement.Subscribe, ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage> subscriptionDispatcher, ITimeProvider timeProvider)
        {
            ProjectionSourceDefinition temp;

            return CreateAndPrepare(
                projectionCorrelationId, publisher, ioDispatcher, subscriptionDispatcher, timeProvider, out temp);
        }

        public CoreProjection CreateAndPrepare(
            Guid projectionCorrelationId, IPublisher publisher, IODispatcher ioDispatcher,
            PublishSubscribeDispatcher<ReaderSubscriptionManagement.Subscribe, ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage> subscriptionDispatcher, ITimeProvider timeProvider,
            out ProjectionSourceDefinition preparedSourceDefinition)
        {
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");
            if (timeProvider == null) throw new ArgumentNullException("timeProvider");

            return InternalCreate(
                projectionCorrelationId, publisher, ioDispatcher, subscriptionDispatcher, timeProvider,
                preparedSourceDefinition: out preparedSourceDefinition);
        }

        public CoreProjection CreatePrepared(
            Guid projectionCorrelationId, IPublisher publisher, IODispatcher ioDispatcher,
            PublishSubscribeDispatcher<ReaderSubscriptionManagement.Subscribe, ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage> subscriptionDispatcher, ITimeProvider timeProvider,
            out ProjectionSourceDefinition preparedSourceDefinition)
        {
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");
            if (timeProvider == null) throw new ArgumentNullException("timeProvider");

            return InternalCreate(
                projectionCorrelationId, publisher, ioDispatcher, subscriptionDispatcher, timeProvider,
                preparedSourceDefinition: out preparedSourceDefinition);
        }

        public EventProcessingProjectionProcessingPhase CreateFirstProcessingPhase(
            IPublisher publisher, Guid projectionCorrelationId, PartitionStateCache partitionStateCache,
            Action updateStatistics, CoreProjection coreProjection, ProjectionNamesBuilder namingBuilder,
            ITimeProvider timeProvider, IODispatcher ioDispatcher)
        {
            var checkpointStrategy = CheckpointStrategy.Create(0, _sourceDefinition, _projectionConfig, timeProvider);

            var resultEmitter = checkpointStrategy.CreateResultEmitter(namingBuilder);
            var zeroCheckpointTag = checkpointStrategy.ReaderStrategy.PositionTagger.MakeZeroCheckpointTag();
            var statePartitionSelector = checkpointStrategy.CreateStatePartitionSelector(_stateHandler);

            var checkpointManager = checkpointStrategy.CreateCheckpointManager(
                projectionCorrelationId, _projectionVersion, publisher, ioDispatcher, _projectionConfig, _name,
                namingBuilder, checkpointStrategy.ReaderStrategy.IsReadingOrderRepeatable, checkpointStrategy._runAs);




            var projectionProcessingPhase = new EventProcessingProjectionProcessingPhase(
                coreProjection, projectionCorrelationId, publisher, this, _projectionConfig, updateStatistics,
                _stateHandler, partitionStateCache, checkpointStrategy._definesStateTransform, _name, _logger,
                zeroCheckpointTag, resultEmitter, checkpointManager, statePartitionSelector, checkpointStrategy,
                timeProvider);
            return projectionProcessingPhase;
        }

    }
}