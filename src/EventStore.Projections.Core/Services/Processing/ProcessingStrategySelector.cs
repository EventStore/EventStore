using EventStore.Projections.Core.Messages;
using ILogger = Serilog.ILogger;

namespace EventStore.Projections.Core.Services.Processing {
	public class ProcessingStrategySelector {
		private readonly ILogger _logger = Serilog.Log.ForContext<ProcessingStrategySelector>();
		private readonly ReaderSubscriptionDispatcher _subscriptionDispatcher;

		public ProcessingStrategySelector(
			ReaderSubscriptionDispatcher subscriptionDispatcher) {
			_subscriptionDispatcher = subscriptionDispatcher;
		}

		public ProjectionProcessingStrategy CreateProjectionProcessingStrategy(
			string name,
			ProjectionVersion projectionVersion,
			ProjectionNamesBuilder namesBuilder,
			IQuerySources sourceDefinition,
			ProjectionConfig projectionConfig,
			IProjectionStateHandler stateHandler, string handlerType, string query) {

			return projectionConfig.StopOnEof
				? (ProjectionProcessingStrategy)
				new QueryProcessingStrategy(
					name,
					projectionVersion,
					stateHandler,
					projectionConfig,
					sourceDefinition,
					_logger,
					_subscriptionDispatcher)
				: new ContinuousProjectionProcessingStrategy(
					name,
					projectionVersion,
					stateHandler,
					projectionConfig,
					sourceDefinition,
					_logger,
					_subscriptionDispatcher);
		}
	}
}
