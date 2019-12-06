using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Common.Log;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.AwakeReaderService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Grpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace EventStore.Projections.Core {
	public sealed class ProjectionsSubsystem :ISubsystem,
		IHandle<SystemMessage.SystemCoreReady>,
		IHandle<SystemMessage.StateChangeMessage>,
		IHandle<CoreProjectionStatusMessage.Stopped>,
		IHandle<ProjectionSubsystemMessage.RestartSubsystem>,
		IHandle<ProjectionSubsystemMessage.ComponentStarted>,
		IHandle<ProjectionSubsystemMessage.ComponentStopped> {
		private static readonly MediaTypeHeaderValue Grpc = new MediaTypeHeaderValue("application/grpc");
		private static readonly PathString ProjectionsSegment = "/event_store.grpc.projections.Projections";

		public InMemoryBus MasterMainBus {
			get { return _masterMainBus; }
		}
		public InMemoryBus MasterOutputBus {
			get { return _masterOutputBus; }
		}
		
		private readonly int _projectionWorkerThreadCount;
		private readonly ProjectionType _runProjections;
		private readonly bool _startStandardProjections;
		private readonly TimeSpan _projectionsQueryExpiry;
		private readonly ILogger _logger = LogManager.GetLoggerFor<ProjectionsSubsystem>();
		public const int VERSION = 3;

		private IQueuedHandler _masterInputQueue;
		private readonly InMemoryBus _masterMainBus;
		private InMemoryBus _masterOutputBus;
		private IDictionary<Guid, IQueuedHandler> _coreQueues;
		private Dictionary<Guid, IPublisher> _queueMap;
		private bool _subsystemStarted;

		private readonly bool _faultOutOfOrderProjections;
		
		private readonly int _componentCount;
		private bool _restarting;
		private int _pendingComponentStarts;
		private int _runningComponentCount;
		
		private VNodeState _nodeState;
		private SubsystemState _subsystemState = SubsystemState.NotReady;
		private Guid _instanceCorrelationId;
		
		public Func<IApplicationBuilder, IApplicationBuilder> Configure => builder => builder
			.UseWhen(
				context => context.Request.Path.StartsWithSegments(ProjectionsSegment),
				inner => inner
					.UseRouting()
					.UseEndpoints(endpoints => endpoints.MapGrpcService<ProjectionManagement>()));

		public Func<IServiceCollection, IServiceCollection> ConfigureServices => services =>
			services.AddSingleton(provider =>
				new ProjectionManagement(_masterInputQueue, provider.GetService<IAuthenticationProvider>()));

		public ProjectionsSubsystem(int projectionWorkerThreadCount, ProjectionType runProjections,
			bool startStandardProjections, TimeSpan projectionQueryExpiry, bool faultOutOfOrderProjections) {
			if (runProjections <= ProjectionType.System)
				_projectionWorkerThreadCount = 1;
			else
				_projectionWorkerThreadCount = projectionWorkerThreadCount;

			_runProjections = runProjections;
			// Projection manager & Projection Core Coordinator
			// The manager only starts when projections are running
			_componentCount = _runProjections == ProjectionType.None ? 1 : 2;

			_startStandardProjections = startStandardProjections;
			_projectionsQueryExpiry = projectionQueryExpiry;
			_faultOutOfOrderProjections = faultOutOfOrderProjections;
			_masterMainBus = new InMemoryBus("manager input bus");
		}

		public void Register(StandardComponents standardComponents) {
			_masterInputQueue = QueuedHandler.CreateQueuedHandler(_masterMainBus, "Projections Master",
				standardComponents.QueueStatsManager);
			_masterOutputBus = new InMemoryBus("ProjectionManagerAndCoreCoordinatorOutput");
			
			_masterMainBus.Subscribe<ProjectionSubsystemMessage.RestartSubsystem>(this);
			_masterMainBus.Subscribe<ProjectionSubsystemMessage.ComponentStarted>(this);
			_masterMainBus.Subscribe<ProjectionSubsystemMessage.ComponentStopped>(this);
			_masterMainBus.Subscribe<SystemMessage.SystemCoreReady>(this);
			_masterMainBus.Subscribe<SystemMessage.StateChangeMessage>(this);
			
			var projectionsStandardComponents = new ProjectionsStandardComponents(
				_projectionWorkerThreadCount,
				_runProjections,
				_masterOutputBus,
				_masterInputQueue,
				_masterMainBus, _faultOutOfOrderProjections);

			CreateAwakerService(standardComponents);
			_coreQueues =
				ProjectionCoreWorkersNode.CreateCoreWorkers(standardComponents, projectionsStandardComponents);
			_queueMap = _coreQueues.ToDictionary(v => v.Key, v => (IPublisher)v.Value);

			ProjectionManagerNode.CreateManagerService(standardComponents, projectionsStandardComponents, _queueMap,
				_projectionsQueryExpiry);
			projectionsStandardComponents.MasterMainBus.Subscribe<CoreProjectionStatusMessage.Stopped>(this);
		}
		
		private static void CreateAwakerService(StandardComponents standardComponents) {
			var awakeReaderService = new AwakeService();
			standardComponents.MainBus.Subscribe<StorageMessage.EventCommitted>(awakeReaderService);
			standardComponents.MainBus.Subscribe<StorageMessage.TfEofAtNonCommitRecord>(awakeReaderService);
			standardComponents.MainBus.Subscribe<AwakeServiceMessage.SubscribeAwake>(awakeReaderService);
			standardComponents.MainBus.Subscribe<AwakeServiceMessage.UnsubscribeAwake>(awakeReaderService);
		}

		public void Handle(SystemMessage.SystemCoreReady message) {
			if (_subsystemState != SubsystemState.NotReady) return;
			_subsystemState = SubsystemState.Ready;
			if (_nodeState == VNodeState.Master)
				StartComponents();
		}

		public void Handle(SystemMessage.StateChangeMessage message) {
			_nodeState = message.State;
			if (_subsystemState == SubsystemState.NotReady) return;
			
			if (_nodeState == VNodeState.Master) {
				StartComponents();
			} else {
				StopComponents();
			}
		}

		private void StartComponents() {
			if (_nodeState != VNodeState.Master) {
				_logger.Debug("PROJECTIONS SUBSYSTEM: Not starting because node is not master. Current node state: {nodeState}",
					_nodeState);
				return;
			}
			if (_subsystemState != SubsystemState.Ready && _subsystemState != SubsystemState.Stopped) {
				_logger.Debug("PROJECTIONS SUBSYSTEM: Not starting because system is not ready or stopped. Current Subsystem state: {subsystemState}",
					_subsystemState);
				return;
			}
			if (_runningComponentCount > 0) {
				_logger.Warn("PROJECTIONS SUBSYSTEM: Subsystem is stopped, but components are still running.");
				return;
			}

			_subsystemState = SubsystemState.Starting;
			_restarting = false;
			_instanceCorrelationId = Guid.NewGuid();
			_logger.Info("PROJECTIONS SUBSYSTEM: Starting components for Instance: {instanceCorrelationId}", _instanceCorrelationId);
			_pendingComponentStarts = _componentCount;
			_masterMainBus.Publish(new ProjectionSubsystemMessage.StartComponents(_instanceCorrelationId));
		}

		private void StopComponents() {
			if (_subsystemState != SubsystemState.Started) {
				_logger.Debug("PROJECTIONS SUBSYSTEM: Not stopping because subsystem is not in a started state. Current Subsystem state: {state}", _subsystemState);
				return;
			}
			
			_logger.Info("PROJECTIONS SUBSYSTEM: Stopping components for Instance: {instanceCorrelationId}", _instanceCorrelationId);
			_subsystemState = SubsystemState.Stopping;
			_masterMainBus.Publish(new ProjectionSubsystemMessage.StopComponents(_instanceCorrelationId));
		}
		
		public void Handle(ProjectionSubsystemMessage.RestartSubsystem message) {
			if (_restarting) {
				_logger.Info("PROJECTIONS SUBSYSTEM: Not restarting because the subsystem is already being restarted.");
				message.ReplyEnvelope.ReplyWith(new ProjectionSubsystemMessage.InvalidSubsystemRestart("Restarting"));
				return;
			}

			if (_subsystemState != SubsystemState.Started) {
				_logger.Info(
					"PROJECTIONS SUBSYSTEM: Not restarting because the subsystem is not started. Current subsystem state: {state}",
					_subsystemState);
				message.ReplyEnvelope.ReplyWith(new ProjectionSubsystemMessage.InvalidSubsystemRestart(_subsystemState.ToString()));
				return;
			}

			_logger.Info("PROJECTIONS SUBSYSTEM: Restarting subsystem.");
			_restarting = true;
			StopComponents();
			message.ReplyEnvelope.ReplyWith(new ProjectionSubsystemMessage.SubsystemRestarting());
		}
		
		public void Handle(ProjectionSubsystemMessage.ComponentStarted message) {
			if (message.InstanceCorrelationId != _instanceCorrelationId) {
				_logger.Debug(
					"PROJECTIONS SUBSYSTEM: Received component started for incorrect instance id. " +
					"Requested: {requestedCorrelationId} | Current: {instanceCorrelationId}",
					message.InstanceCorrelationId, _instanceCorrelationId);
				return;
			}

			if (_pendingComponentStarts <= 0 || _subsystemState != SubsystemState.Starting)
				return;
			
			_logger.Debug("PROJECTIONS SUBSYSTEM: Component '{componentName}' started for Instance: {instanceCorrelationId}",
				message.ComponentName, message.InstanceCorrelationId);
			_pendingComponentStarts--;
			_runningComponentCount++;
				
			if (_pendingComponentStarts == 0) {
				AllComponentsStarted();
			}
		}

		private void AllComponentsStarted() {
			_logger.Info("PROJECTIONS SUBSYSTEM: All components started for Instance: {instanceCorrelationId}",
				_instanceCorrelationId);
			_subsystemState = SubsystemState.Started;
			_masterOutputBus.Publish(new SystemMessage.SubSystemInitialized("Projections"));

			if (_nodeState != VNodeState.Master) {
				_logger.Info("PROJECTIONS SUBSYSTEM: Node state is no longer Master. Stopping projections. Current node state: {nodeState}",
					_nodeState);
				StopComponents();
			}
		}

		public void Handle(ProjectionSubsystemMessage.ComponentStopped message) {
			if (message.InstanceCorrelationId != _instanceCorrelationId) {
				_logger.Debug(
					"PROJECTIONS SUBSYSTEM: Received component stopped for incorrect correlation id. " +
					"Requested: {requestedCorrelationId} | Instance: {instanceCorrelationId}",
					message.InstanceCorrelationId, _instanceCorrelationId);
				return;
			}

			if (_subsystemState != SubsystemState.Stopping)
				return;

			_logger.Debug("PROJECTIONS SUBSYSTEM: Component '{componentName}' stopped for Instance: {instanceCorrelationId}",
				message.ComponentName, message.InstanceCorrelationId);
			_runningComponentCount--;
			if (_runningComponentCount < 0) {
				_logger.Warn("PROJECTIONS SUBSYSTEM: Got more component stopped messages than running components.");
				_runningComponentCount = 0;
			}

			if (_runningComponentCount == 0) {
				AllComponentsStopped();
			}
		}

		private void AllComponentsStopped() {
			_logger.Info("PROJECTIONS SUBSYSTEM: All components stopped for Instance: {instanceCorrelationId}",
				_instanceCorrelationId);
			_subsystemState = SubsystemState.Stopped;
			
			if (_restarting) {
				StartComponents();
				return;
			}

			if (_nodeState == VNodeState.Master) {
				_logger.Info("PROJECTIONS SUBSYSTEM: Node state has changed to Master. Starting projections.");
				StartComponents();
			}
		}

		public IEnumerable<Task> Start() {
			var tasks = new List<Task>();
			if (_subsystemStarted == false) {
				if (_masterInputQueue != null)
					tasks.Add(_masterInputQueue.Start());
				foreach (var queue in _coreQueues)
					tasks.Add(queue.Value.Start());
			}

			_subsystemStarted = true;
			return tasks;
		}

		public void Stop() {
			if (_subsystemStarted) {
				if (_masterInputQueue != null)
					_masterInputQueue.Stop();
				foreach (var queue in _coreQueues)
					queue.Value.Stop();
			}

			_subsystemStarted = false;
		}

		private readonly List<string> _standardProjections = new List<string> {
			"$by_category",
			"$stream_by_category",
			"$streams",
			"$by_event_type",
			"$by_correlation_id"
		};

		public void Handle(CoreProjectionStatusMessage.Stopped message) {
			if (_startStandardProjections) {
				if (_standardProjections.Contains(message.Name)) {
					_standardProjections.Remove(message.Name);
					var envelope = new NoopEnvelope();
					_masterMainBus.Publish(new ProjectionManagementMessage.Command.Enable(envelope, message.Name,
						ProjectionManagementMessage.RunAs.System));
				}
			}
		}

		private enum SubsystemState {
			NotReady,
			Ready,
			Starting,
			Started,
			Stopping,
			Stopped
		}

		private static bool IsGrpc(HttpContext context) =>
			context.Request.Headers.TryGetValue("content-type", out var contentType) &&
			MediaTypeHeaderValue.TryParse(new StringSegment(contentType), out var contentTypeHeader) &&
			contentTypeHeader.Type == Grpc.Type &&
			contentTypeHeader.SubTypeWithoutSuffix == Grpc.SubTypeWithoutSuffix;

	}
}
