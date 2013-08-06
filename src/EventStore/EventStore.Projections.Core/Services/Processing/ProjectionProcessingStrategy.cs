using System;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ProjectionProcessingStrategy
    {
        private CoreProjection InternalCreate(
            string name, ProjectionVersion version, Guid projectionCorrelationId, IPublisher publisher,
            IProjectionStateHandler projectionStateHandler, ProjectionConfig projectionConfig, IODispatcher ioDispatcher,
            PublishSubscribeDispatcher<ReaderSubscriptionManagement.Subscribe, ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage> subscriptionDispatcher, ILogger logger, ITimeProvider timeProvider,
            ISourceDefinitionConfigurator sourceDefinition, out ProjectionSourceDefinition preparedSourceDefinition)
        {
            var builder = new CheckpointStrategy.Builder();
            var namingBuilderFactory = new ProjectionNamesBuilder.Factory();
            sourceDefinition.ConfigureSourceProcessingStrategy(builder);
            sourceDefinition.ConfigureSourceProcessingStrategy(namingBuilderFactory);
            var namingBuilder = namingBuilderFactory.Create(name);
            var effectiveProjectionName = namingBuilder.EffectiveProjectionName;

            var checkpointStrategy = CheckpointStrategy.Create(0, sourceDefinition, projectionConfig, timeProvider);
            var sourceDefinitionRecorder = new SourceDefinitionRecorder();
            (projectionStateHandler ?? sourceDefinition).ConfigureSourceProcessingStrategy(sourceDefinitionRecorder);
            preparedSourceDefinition = sourceDefinitionRecorder.Build(namingBuilder);
            return new CoreProjection(
                effectiveProjectionName, version, projectionCorrelationId, publisher, projectionStateHandler,
                projectionConfig, ioDispatcher, subscriptionDispatcher, logger, checkpointStrategy, namingBuilder, this);
        }

        public static CoreProjection CreateAndPrepare(
            string name, ProjectionVersion version, Guid projectionCorrelationId, IPublisher publisher,
            IProjectionStateHandler projectionStateHandler, ProjectionConfig projectionConfig, IODispatcher ioDispatcher,
            PublishSubscribeDispatcher<ReaderSubscriptionManagement.Subscribe, ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage> subscriptionDispatcher, ILogger logger, ITimeProvider timeProvider)
        {
            if (name == null) throw new ArgumentNullException("name");
            if (name == "") throw new ArgumentException("name");
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (projectionStateHandler == null) throw new ArgumentNullException("projectionStateHandler");
            if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");
            if (timeProvider == null) throw new ArgumentNullException("timeProvider");

            ProjectionSourceDefinition temp;

            return new ProjectionProcessingStrategy().InternalCreate(
                name, version, projectionCorrelationId, publisher, projectionStateHandler, projectionConfig,
                ioDispatcher, subscriptionDispatcher, logger, timeProvider, sourceDefinition: projectionStateHandler,
                preparedSourceDefinition: out temp);
        }

        public static CoreProjection CreateAndPrepare(
            string name, ProjectionVersion version, Guid projectionCorrelationId, IPublisher publisher,
            IProjectionStateHandler projectionStateHandler, ProjectionConfig projectionConfig, IODispatcher ioDispatcher,
            PublishSubscribeDispatcher<ReaderSubscriptionManagement.Subscribe, ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage> subscriptionDispatcher, ILogger logger, ITimeProvider timeProvider,
            out ProjectionSourceDefinition preparedSourceDefinition)
        {
            if (name == null) throw new ArgumentNullException("name");
            if (name == "") throw new ArgumentException("name");
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (projectionStateHandler == null) throw new ArgumentNullException("projectionStateHandler");
            if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");
            if (timeProvider == null) throw new ArgumentNullException("timeProvider");

            return new ProjectionProcessingStrategy().InternalCreate(
                name, version, projectionCorrelationId, publisher, projectionStateHandler, projectionConfig,
                ioDispatcher, subscriptionDispatcher, logger, timeProvider, sourceDefinition: projectionStateHandler,
                preparedSourceDefinition: out preparedSourceDefinition);
        }

        public static CoreProjection CreatePrepared(
            string name, ProjectionVersion version, Guid projectionCorrelationId, IPublisher publisher,
            ISourceDefinitionConfigurator sourceDefinition, ProjectionConfig projectionConfig, IODispatcher ioDispatcher,
            PublishSubscribeDispatcher<ReaderSubscriptionManagement.Subscribe, ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage> subscriptionDispatcher, ILogger logger, ITimeProvider timeProvider,
            out ProjectionSourceDefinition preparedSourceDefinition)
        {
            if (name == null) throw new ArgumentNullException("name");
            if (name == "") throw new ArgumentException("name");
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");
            if (timeProvider == null) throw new ArgumentNullException("timeProvider");

            return new ProjectionProcessingStrategy().InternalCreate(
                name, version, projectionCorrelationId, publisher, null, projectionConfig, ioDispatcher,
                subscriptionDispatcher, logger, timeProvider, sourceDefinition: sourceDefinition,
                preparedSourceDefinition: out preparedSourceDefinition);
        }

        public EventProcessingProjectionProcessingPhase CreateFirstProcessingPhase(CheckpointStrategy checkpointStrategy, string name, IPublisher publisher, IProjectionStateHandler projectionStateHandler,
            ProjectionConfig projectionConfig, ILogger logger, Guid projectionCorrelationId,
            PartitionStateCache partitionStateCache, Action updateStatistics, CoreProjection coreProjection,
            ProjectionNamesBuilder namingBuilder, ICoreProjectionCheckpointManager checkpointManager,
            StatePartitionSelector statePartitionSelector)
        {
            var resultEmitter = checkpointStrategy.CreateResultEmitter(namingBuilder);
            var zeroCheckpointTag = checkpointStrategy.ReaderStrategy.PositionTagger.MakeZeroCheckpointTag();
            var projectionProcessingPhase = new EventProcessingProjectionProcessingPhase(
                coreProjection, projectionCorrelationId, publisher, projectionConfig, updateStatistics,
                projectionStateHandler, partitionStateCache, checkpointStrategy._definesStateTransform, name, logger, zeroCheckpointTag,
                resultEmitter, checkpointManager, statePartitionSelector, checkpointStrategy);
            return projectionProcessingPhase;
        }
    }
}