using System;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public class ParallelQueryProcessingStrategy : EventReaderBasedProjectionProcessingStrategy {
		private readonly IProjectionStateHandler _stateHandler;
		private new readonly ProjectionConfig _projectionConfig;
		private new readonly IQuerySources _sourceDefinition;
		private readonly ProjectionNamesBuilder _namesBuilder;

		private readonly SpooledStreamReadingDispatcher _spoolProcessingResponseDispatcher;
		private readonly string _catalogStreamName;
		private readonly string _handlerType;
		private readonly string _query;

		public ParallelQueryProcessingStrategy(
			string name,
			ProjectionVersion projectionVersion,
			IProjectionStateHandler stateHandler,
			ProjectionConfig projectionConfig,
			IQuerySources sourceDefinition,
			string handlerType,
			string query,
			ProjectionNamesBuilder namesBuilder,
			ILogger logger,
			SpooledStreamReadingDispatcher spoolProcessingResponseDispatcher,
			ReaderSubscriptionDispatcher subscriptionDispatcher)
			: base(name, projectionVersion, projectionConfig, sourceDefinition, logger, subscriptionDispatcher) {
			if (string.IsNullOrEmpty(handlerType)) throw new ArgumentNullException("handlerType");
			if (string.IsNullOrEmpty(query)) throw new ArgumentNullException("query");

			_stateHandler = stateHandler;
			_projectionConfig = projectionConfig;
			_sourceDefinition = sourceDefinition;
			_handlerType = handlerType;
			_query = query;
			_namesBuilder = namesBuilder;
			_spoolProcessingResponseDispatcher = spoolProcessingResponseDispatcher;
			if (_sourceDefinition.CatalogStream == SystemStreams.AllStream) {
				_catalogStreamName = SystemStreams.AllStream;
			} else if (_sourceDefinition.HasCategories()) {
				_catalogStreamName = _namesBuilder.GetCategoryCatalogStreamName(_sourceDefinition.Categories[0]);
			} else {
				_catalogStreamName = _sourceDefinition.CatalogStream;
			}
		}

		protected override IResultEventEmitter CreateFirstPhaseResultEmitter(ProjectionNamesBuilder namingBuilder) {
			return new ResultEventEmitter(namingBuilder);
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
			var coreProjectionCheckpointWriter =
				new CoreProjectionCheckpointWriter(
					namingBuilder.MakeCheckpointStreamName(),
					ioDispatcher,
					_projectionVersion,
					_name);
			var checkpointManager2 = new DefaultCheckpointManager(
				publisher,
				projectionCorrelationId,
				_projectionVersion,
				_projectionConfig.RunAs,
				ioDispatcher,
				_projectionConfig,
				_name,
				new PhasePositionTagger(1),
				namingBuilder,
				GetUseCheckpoints(),
				false,
				_sourceDefinition.DefinesFold,
				coreProjectionCheckpointWriter);

			var writeResultsPhase = new WriteQueryEofProjectionProcessingPhase(
				publisher,
				1,
				namingBuilder.GetResultStreamName(),
				coreProjection,
				partitionStateCache,
				checkpointManager2,
				checkpointManager2,
				firstPhase.EmittedStreamsTracker);

			return new[] {firstPhase, writeResultsPhase};
		}

		protected override IReaderStrategy CreateReaderStrategy(ITimeProvider timeProvider) {
			if (_catalogStreamName == SystemStreams.AllStream) {
				return new ParallelQueryAllStreamsMasterReaderStrategy(_name, 0, SystemAccount.Principal, timeProvider);
			}

			return new ParallelQueryMasterReaderStrategy(
				_name,
				0,
				SystemAccount.Principal,
				timeProvider,
				_catalogStreamName);
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
			return new ParallelQueryMasterProjectionProcessingPhase(
				coreProjection,
				projectionCorrelationId,
				publisher,
				inputQueue,
				_projectionConfig,
				updateStatistics,
				_stateHandler,
				partitionStateCache,
				_name,
				_logger,
				zeroCheckpointTag,
				checkpointManager,
				subscriptionDispatcher,
				readerStrategy,
				resultWriter,
				_projectionConfig.CheckpointsEnabled,
				this.GetStopOnEof(),
				_spoolProcessingResponseDispatcher,
				emittedStreamsTracker);
		}

		public override bool GetStopOnEof() {
			return true;
		}

		public override bool GetUseCheckpoints() {
			return _projectionConfig.CheckpointsEnabled;
		}

		public override bool GetRequiresRootPartition() {
			return false;
		}

		public override bool GetProducesRunningResults() {
			return false;
		}

		public override bool GetIsSlaveProjection() {
			return false;
		}

		public override void EnrichStatistics(ProjectionStatistics info) {
		}

		public override SlaveProjectionDefinitions GetSlaveProjections() {
			return
				new SlaveProjectionDefinitions(
					new SlaveProjectionDefinitions.Definition(
						"slave", _handlerType, _query,
						SlaveProjectionDefinitions.SlaveProjectionRequestedNumber.OnePerThread,
						ProjectionMode.Transient,
						_projectionConfig.EmitEventEnabled, _projectionConfig.CheckpointsEnabled,
						trackEmittedStreams: _projectionConfig.TrackEmittedStreams,
						runAs1: new ProjectionManagementMessage.RunAs(_projectionConfig.RunAs), enableRunAs: true));
		}
	}
}
