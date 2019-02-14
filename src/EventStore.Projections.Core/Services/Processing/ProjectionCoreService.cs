using System;
using System.Collections.Generic;
using System.Security.Principal;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Services.Management;
using EventStore.Common.Utils;

namespace EventStore.Projections.Core.Services.Processing {
	public class ProjectionCoreService
		: IHandle<ProjectionCoreServiceMessage.StartCore>,
			IHandle<ProjectionCoreServiceMessage.StopCore>,
			IHandle<ProjectionCoreServiceMessage.CoreTick>,
			IHandle<CoreProjectionManagementMessage.CreateAndPrepare>,
			IHandle<CoreProjectionManagementMessage.CreatePrepared>,
			IHandle<CoreProjectionManagementMessage.CreateAndPrepareSlave>,
			IHandle<CoreProjectionManagementMessage.Dispose>,
			IHandle<CoreProjectionManagementMessage.Start>,
			IHandle<CoreProjectionManagementMessage.LoadStopped>,
			IHandle<CoreProjectionManagementMessage.Stop>,
			IHandle<CoreProjectionManagementMessage.Kill>,
			IHandle<CoreProjectionManagementMessage.GetState>,
			IHandle<CoreProjectionManagementMessage.GetResult>,
			IHandle<ProjectionManagementMessage.SlaveProjectionsStarted>,
			IHandle<CoreProjectionProcessingMessage.CheckpointCompleted>,
			IHandle<CoreProjectionProcessingMessage.CheckpointLoaded>,
			IHandle<CoreProjectionProcessingMessage.PrerecordedEventsLoaded>,
			IHandle<CoreProjectionProcessingMessage.RestartRequested>,
			IHandle<CoreProjectionProcessingMessage.Failed> {
		private readonly Guid _workerId;
		private readonly IPublisher _publisher;
		private readonly IPublisher _inputQueue;
		private readonly ILogger _logger = LogManager.GetLoggerFor<ProjectionCoreService>();

		private readonly Dictionary<Guid, CoreProjection> _projections = new Dictionary<Guid, CoreProjection>();

		private readonly IODispatcher _ioDispatcher;

		private readonly ReaderSubscriptionDispatcher _subscriptionDispatcher;

		private readonly ITimeProvider _timeProvider;
		private readonly ProcessingStrategySelector _processingStrategySelector;

		private readonly SpooledStreamReadingDispatcher _spoolProcessingResponseDispatcher;
		private readonly ISingletonTimeoutScheduler _timeoutScheduler;


		public ProjectionCoreService(
			Guid workerId,
			IPublisher inputQueue,
			IPublisher publisher,
			ReaderSubscriptionDispatcher subscriptionDispatcher,
			ITimeProvider timeProvider,
			IODispatcher ioDispatcher,
			SpooledStreamReadingDispatcher spoolProcessingResponseDispatcher,
			ISingletonTimeoutScheduler timeoutScheduler) {
			_workerId = workerId;
			_inputQueue = inputQueue;
			_publisher = publisher;
			_ioDispatcher = ioDispatcher;
			_spoolProcessingResponseDispatcher = spoolProcessingResponseDispatcher;
			_timeoutScheduler = timeoutScheduler;
			_subscriptionDispatcher = subscriptionDispatcher;
			_timeProvider = timeProvider;
			_processingStrategySelector = new ProcessingStrategySelector(
				_subscriptionDispatcher,
				_spoolProcessingResponseDispatcher);
		}

		public ILogger Logger {
			get { return _logger; }
		}

		public void Handle(ProjectionCoreServiceMessage.StartCore message) {
			_publisher.Publish(new ProjectionCoreServiceMessage.SubComponentStarted("ProjectionCoreService"));
		}

		public void Handle(ProjectionCoreServiceMessage.StopCore message) {
			StopProjections();
			_publisher.Publish(new ProjectionCoreServiceMessage.SubComponentStopped("ProjectionCoreService"));
		}

		private void StopProjections() {
			_ioDispatcher.BackwardReader.CancelAll();
			_ioDispatcher.ForwardReader.CancelAll();
			_ioDispatcher.Writer.CancelAll();

			var allProjections = _projections.Values;
			foreach (var projection in allProjections)
				projection.Kill();

			if (_projections.Count > 0) {
				_logger.Info("_projections is not empty after all the projections have been killed");
				_projections.Clear();
			}
		}

		public void Handle(ProjectionCoreServiceMessage.CoreTick message) {
			message.Action();
		}

		public void Handle(CoreProjectionManagementMessage.CreateAndPrepare message) {
			try {
				//TODO: factory method can throw
				var stateHandler = CreateStateHandler(
					_timeoutScheduler,
					_logger,
					message.HandlerType,
					message.Query);

				string name = message.Name;
				var sourceDefinition = ProjectionSourceDefinition.From(stateHandler.GetSourceDefinition());

				var projectionVersion = message.Version;
				var projectionConfig = message.Config;
				var namesBuilder = new ProjectionNamesBuilder(name, sourceDefinition);

				var projectionProcessingStrategy = _processingStrategySelector.CreateProjectionProcessingStrategy(
					name,
					projectionVersion,
					namesBuilder,
					sourceDefinition,
					projectionConfig,
					stateHandler,
					message.HandlerType,
					message.Query);

				CreateCoreProjection(message.ProjectionId, projectionConfig.RunAs, projectionProcessingStrategy);
				_publisher.Publish(
					new CoreProjectionStatusMessage.Prepared(
						message.ProjectionId, sourceDefinition));
			} catch (Exception ex) {
				_publisher.Publish(
					new CoreProjectionStatusMessage.Faulted(message.ProjectionId, ex.Message));
			}
		}

		public void Handle(CoreProjectionManagementMessage.CreatePrepared message) {
			try {
				var name = message.Name;
				var sourceDefinition = ProjectionSourceDefinition.From(message.SourceDefinition);
				var projectionVersion = message.Version;
				var projectionConfig = message.Config;
				var namesBuilder = new ProjectionNamesBuilder(name, sourceDefinition);

				var projectionProcessingStrategy = _processingStrategySelector.CreateProjectionProcessingStrategy(
					name,
					projectionVersion,
					namesBuilder,
					sourceDefinition,
					projectionConfig,
					null,
					message.HandlerType,
					message.Query);

				CreateCoreProjection(message.ProjectionId, projectionConfig.RunAs, projectionProcessingStrategy);
				_publisher.Publish(
					new CoreProjectionStatusMessage.Prepared(
						message.ProjectionId, sourceDefinition));
			} catch (Exception ex) {
				_publisher.Publish(
					new CoreProjectionStatusMessage.Faulted(message.ProjectionId, ex.Message));
			}
		}

		public void Handle(CoreProjectionManagementMessage.CreateAndPrepareSlave message) {
			try {
				var stateHandler = CreateStateHandler(_timeoutScheduler, _logger, message.HandlerType, message.Query);

				string name = message.Name;
				var sourceDefinition = ProjectionSourceDefinition.From(stateHandler.GetSourceDefinition());
				var projectionVersion = message.Version;
				var projectionConfig = message.Config.SetIsSlave();
				var projectionProcessingStrategy =
					_processingStrategySelector.CreateSlaveProjectionProcessingStrategy(
						name,
						projectionVersion,
						sourceDefinition,
						projectionConfig,
						stateHandler,
						message.MasterWorkerId,
						_publisher,
						message.MasterCoreProjectionId,
						this);
				CreateCoreProjection(message.ProjectionId, projectionConfig.RunAs, projectionProcessingStrategy);
				_publisher.Publish(
					new CoreProjectionStatusMessage.Prepared(
						message.ProjectionId,
						sourceDefinition));
			} catch (Exception ex) {
				_publisher.Publish(new CoreProjectionStatusMessage.Faulted(message.ProjectionId, ex.Message));
			}
		}

		private void CreateCoreProjection(
			Guid projectionCorrelationId, IPrincipal runAs, ProjectionProcessingStrategy processingStrategy) {
			var projection = processingStrategy.Create(
				projectionCorrelationId,
				_inputQueue,
				_workerId,
				runAs,
				_publisher,
				_ioDispatcher,
				_subscriptionDispatcher,
				_timeProvider);
			_projections.Add(projectionCorrelationId, projection);
		}

		public void Handle(CoreProjectionManagementMessage.Dispose message) {
			CoreProjection projection;
			if (_projections.TryGetValue(message.ProjectionId, out projection)) {
				_projections.Remove(message.ProjectionId);
				projection.Dispose();
			}
		}

		public void Handle(CoreProjectionManagementMessage.Start message) {
			var projection = _projections[message.ProjectionId];
			projection.Start();
		}

		public void Handle(CoreProjectionManagementMessage.LoadStopped message) {
			var projection = _projections[message.ProjectionId];
			projection.LoadStopped();
		}

		public void Handle(CoreProjectionManagementMessage.Stop message) {
			var projection = _projections[message.ProjectionId];
			projection.Stop();
		}

		public void Handle(CoreProjectionManagementMessage.Kill message) {
			var projection = _projections[message.ProjectionId];
			projection.Kill();
		}

		public void Handle(CoreProjectionManagementMessage.GetState message) {
			CoreProjection projection;
			if (_projections.TryGetValue(message.ProjectionId, out projection))
				projection.Handle(message);
		}

		public void Handle(CoreProjectionManagementMessage.GetResult message) {
			CoreProjection projection;
			if (_projections.TryGetValue(message.ProjectionId, out projection))
				projection.Handle(message);
		}

		public void Handle(CoreProjectionProcessingMessage.CheckpointCompleted message) {
			CoreProjection projection;
			if (_projections.TryGetValue(message.ProjectionId, out projection))
				projection.Handle(message);
		}

		public void Handle(CoreProjectionProcessingMessage.CheckpointLoaded message) {
			CoreProjection projection;
			if (_projections.TryGetValue(message.ProjectionId, out projection))
				projection.Handle(message);
		}

		public void Handle(CoreProjectionProcessingMessage.PrerecordedEventsLoaded message) {
			CoreProjection projection;
			if (_projections.TryGetValue(message.ProjectionId, out projection))
				projection.Handle(message);
		}

		public void Handle(CoreProjectionProcessingMessage.RestartRequested message) {
			CoreProjection projection;
			if (_projections.TryGetValue(message.ProjectionId, out projection))
				projection.Handle(message);
		}

		public void Handle(CoreProjectionProcessingMessage.Failed message) {
			CoreProjection projection;
			if (_projections.TryGetValue(message.ProjectionId, out projection))
				projection.Handle(message);
		}

		public void Handle(ProjectionManagementMessage.SlaveProjectionsStarted message) {
			CoreProjection projection;
			if (_projections.TryGetValue(message.CoreProjectionCorrelationId, out projection))
				projection.Handle(message);
		}

		public static IProjectionStateHandler CreateStateHandler(
			ISingletonTimeoutScheduler singletonTimeoutScheduler,
			ILogger logger,
			string handlerType,
			string query) {
			var stateHandler = new ProjectionStateHandlerFactory().Create(
				handlerType,
				query,
				logger: logger.Trace,
				cancelCallbackFactory:
				singletonTimeoutScheduler == null ? (Action<int, Action>)null : singletonTimeoutScheduler.Schedule);
			return stateHandler;
		}
	}
}
