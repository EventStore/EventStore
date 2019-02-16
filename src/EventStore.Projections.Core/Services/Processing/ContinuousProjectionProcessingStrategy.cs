using System;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public class ContinuousProjectionProcessingStrategy : DefaultProjectionProcessingStrategy {
		public ContinuousProjectionProcessingStrategy(
			string name, ProjectionVersion projectionVersion, IProjectionStateHandler stateHandler,
			ProjectionConfig projectionConfig, IQuerySources sourceDefinition, ILogger logger,
			ReaderSubscriptionDispatcher subscriptionDispatcher)
			: base(
				name, projectionVersion, stateHandler, projectionConfig, sourceDefinition, logger,
				subscriptionDispatcher) {
		}

		public override bool GetStopOnEof() {
			return false;
		}

		public override bool GetUseCheckpoints() {
			return _projectionConfig.CheckpointsEnabled;
		}

		public override bool GetProducesRunningResults() {
			return _sourceDefinition.ProducesResults;
		}

		public override bool GetIsSlaveProjection() {
			return false;
		}

		public override SlaveProjectionDefinitions GetSlaveProjections() {
			return null;
		}

		protected override IProjectionProcessingPhase[] CreateProjectionProcessingPhases(
			IPublisher publisher,
			IPublisher inputQueue,
			Guid projectionCorrelationId,
			ProjectionNamesBuilder namingBuilder,
			PartitionStateCache partitionStateCache,
			CoreProjection coreProjection,
			IODispatcher ioDispatcher,
			IProjectionProcessingPhase firstPhase) {
			return new IProjectionProcessingPhase[] {firstPhase};
		}

		protected override IResultEventEmitter CreateFirstPhaseResultEmitter(ProjectionNamesBuilder namingBuilder) {
			return _sourceDefinition.ProducesResults
				? new ResultEventEmitter(namingBuilder)
				: (IResultEventEmitter)new NoopResultEventEmitter();
		}
	}
}
