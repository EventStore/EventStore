using System;
using System.Security.Claims;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using ILogger = Serilog.ILogger;

namespace EventStore.Projections.Core.Services.Processing {
	public abstract class ProjectionProcessingStrategy {
		protected readonly string _name;
		protected readonly ProjectionVersion _projectionVersion;
		protected readonly ILogger _logger;

		protected ProjectionProcessingStrategy(string name, ProjectionVersion projectionVersion, ILogger logger) {
			_name = name;
			_projectionVersion = projectionVersion;
			_logger = logger;
		}

		public CoreProjection Create(
			Guid projectionCorrelationId,
			IPublisher inputQueue,
			Guid workerId,
			ClaimsPrincipal runAs,
			IPublisher publisher,
			IODispatcher ioDispatcher,
			ReaderSubscriptionDispatcher subscriptionDispatcher,
			ITimeProvider timeProvider) {
			if (inputQueue == null) throw new ArgumentNullException("inputQueue");
			//if (runAs == null) throw new ArgumentNullException("runAs");
			if (publisher == null) throw new ArgumentNullException("publisher");
			if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");
			if (timeProvider == null) throw new ArgumentNullException("timeProvider");

			var namingBuilder = new ProjectionNamesBuilder(_name, GetSourceDefinition());

			var coreProjectionCheckpointWriter =
				new CoreProjectionCheckpointWriter(
					namingBuilder.MakeCheckpointStreamName(),
					ioDispatcher,
					_projectionVersion,
					namingBuilder.EffectiveProjectionName);

			var partitionStateCache = new PartitionStateCache();

			return new CoreProjection(
				this,
				_projectionVersion,
				projectionCorrelationId,
				inputQueue,
				workerId,
				runAs,
				publisher,
				ioDispatcher,
				subscriptionDispatcher,
				_logger,
				namingBuilder,
				coreProjectionCheckpointWriter,
				partitionStateCache,
				namingBuilder.EffectiveProjectionName,
				timeProvider);
		}

		protected abstract IQuerySources GetSourceDefinition();

		public abstract bool GetStopOnEof();
		public abstract bool GetUseCheckpoints();
		public abstract bool GetRequiresRootPartition();
		public abstract bool GetProducesRunningResults();
		public abstract void EnrichStatistics(ProjectionStatistics info);

		public abstract IProjectionProcessingPhase[] CreateProcessingPhases(
			IPublisher publisher,
			IPublisher inputQueue,
			Guid projectionCorrelationId,
			PartitionStateCache partitionStateCache,
			Action updateStatistics,
			CoreProjection coreProjection,
			ProjectionNamesBuilder namingBuilder,
			ITimeProvider timeProvider,
			IODispatcher ioDispatcher,
			CoreProjectionCheckpointWriter coreProjectionCheckpointWriter);
	}
}
