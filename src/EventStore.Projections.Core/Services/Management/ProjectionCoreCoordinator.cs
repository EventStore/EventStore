using System;
using EventStore.Common.Options;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using EventStore.Common.Log;

namespace EventStore.Projections.Core.Services.Management {
	public class ProjectionCoreCoordinator
		: IHandle<ProjectionManagementMessage.Internal.RegularTimeout>,
			IHandle<SystemMessage.StateChangeMessage>,
			IHandle<SystemMessage.SystemCoreReady>,
			IHandle<SystemMessage.EpochWritten>,
			IHandle<ProjectionCoreServiceMessage.SubComponentStarted>,
			IHandle<ProjectionCoreServiceMessage.SubComponentStopped> {
		private readonly ILogger Log = LogManager.GetLoggerFor<ProjectionCoreCoordinator>();
		private readonly ProjectionType _runProjections;
		private readonly TimeoutScheduler[] _timeoutSchedulers;
		private readonly IPublisher[] _queues;
		private bool _started;
		private readonly IPublisher _publisher;
		private readonly IEnvelope _publishEnvelope;

		private int _pendingSubComponentsStarts = 0;
		private int _activeSubComponents = 0;
		private bool _newInstanceWaiting = false;

		public ProjectionCoreCoordinator(
			ProjectionType runProjections,
			TimeoutScheduler[] timeoutSchedulers,
			IPublisher[] queues,
			IPublisher publisher,
			IEnvelope publishEnvelope) {
			_runProjections = runProjections;
			_timeoutSchedulers = timeoutSchedulers;
			_queues = queues;
			_publisher = publisher;
			_publishEnvelope = publishEnvelope;
		}

		public void Handle(ProjectionManagementMessage.Internal.RegularTimeout message) {
			ScheduleRegularTimeout();
			for (var i = 0; i < _timeoutSchedulers.Length; i++)
				_timeoutSchedulers[i].Tick();
		}

		private bool _systemReady = false;
		private bool _ready = false;

		private VNodeState _currentState = VNodeState.Unknown;
		private Guid _epochId = Guid.Empty;

		public void Handle(SystemMessage.SystemCoreReady message) {
			_systemReady = true;
			StartWhenConditionsAreMet();
		}

		public void Handle(SystemMessage.StateChangeMessage message) {
			_currentState = message.State;
			if (_currentState != VNodeState.Master)
				_ready = false;

			StartWhenConditionsAreMet();
		}

		public void Handle(SystemMessage.EpochWritten message) {
			if (_ready) return;

			if (_currentState == VNodeState.Master) {
				_epochId = message.Epoch.EpochId;
				_ready = true;
			}

			StartWhenConditionsAreMet();
		}

		private void StartWhenConditionsAreMet() {
			//run if and only if these conditions are met
			if (_systemReady && _ready) {
				if (!_started) {
					if (_pendingSubComponentsStarts + _activeSubComponents != 0) {
						_newInstanceWaiting = true;
						return;
					} else {
						_newInstanceWaiting = false;
						_pendingSubComponentsStarts = 0;
						_activeSubComponents = 0;
					}

					Log.Debug("PROJECTIONS: Starting Projections Core Coordinator. (Node State : {state})",
						_currentState);
					Start();
				}
			} else {
				if (_started) {
					Log.Debug("PROJECTIONS: Stopping Projections Core Coordinator. (Node State : {state})",
						_currentState);
					Stop();
				}
			}
		}

		private void ScheduleRegularTimeout() {
			if (!_started)
				return;
			_publisher.Publish(
				TimerMessage.Schedule.Create(
					TimeSpan.FromMilliseconds(100),
					_publishEnvelope,
					new ProjectionManagementMessage.Internal.RegularTimeout()));
		}

		private void Start() {
			if (_started)
				throw new InvalidOperationException();
			_started = true;
			ScheduleRegularTimeout();
			foreach (var queue in _queues) {
				queue.Publish(new ReaderCoreServiceMessage.StartReader());
				_pendingSubComponentsStarts += 1 /*EventReaderCoreService*/;

				if (_runProjections >= ProjectionType.System) {
					queue.Publish(new ProjectionCoreServiceMessage.StartCore(_epochId));
					_pendingSubComponentsStarts += 1 /*ProjectionCoreService*/
					                               + 1 /*ProjectionCoreServiceCommandReader*/;
				} else {
					_publisher.Publish(new SystemMessage.SubSystemInitialized("Projections"));
				}
			}
		}

		private void Stop() {
			if (_started) {
				_started = false;
				foreach (var queue in _queues) {
					queue.Publish(new ReaderCoreServiceMessage.StopReader());
					if (_runProjections >= ProjectionType.System)
						queue.Publish(new ProjectionCoreServiceMessage.StopCore());
				}
			}
		}

		public void Handle(ProjectionCoreServiceMessage.SubComponentStarted message) {
			_pendingSubComponentsStarts--;
			_activeSubComponents++;
			Log.Debug("PROJECTIONS: SubComponent Started: {subComponent}", message.SubComponent);

			if (_newInstanceWaiting)
				StartWhenConditionsAreMet();
		}

		public void Handle(ProjectionCoreServiceMessage.SubComponentStopped message) {
			_activeSubComponents--;
			Log.Debug("PROJECTIONS: SubComponent Stopped: {subComponent}", message.SubComponent);

			if (_newInstanceWaiting)
				StartWhenConditionsAreMet();
		}

		public void SetupMessaging(IBus bus) {
			bus.Subscribe<SystemMessage.StateChangeMessage>(this);
			bus.Subscribe<SystemMessage.SystemCoreReady>(this);
			bus.Subscribe<SystemMessage.EpochWritten>(this);
			bus.Subscribe<ProjectionCoreServiceMessage.SubComponentStarted>(this);
			bus.Subscribe<ProjectionCoreServiceMessage.SubComponentStopped>(this);
			if (_runProjections >= ProjectionType.System) {
				bus.Subscribe<ProjectionManagementMessage.Internal.RegularTimeout>(this);
			}
		}
	}
}
