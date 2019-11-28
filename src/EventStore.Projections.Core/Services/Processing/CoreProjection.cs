using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Utils;

namespace EventStore.Projections.Core.Services.Processing {
	//TODO: replace Console.WriteLine with logging
	//TODO: separate check-pointing from projection handling

	public class CoreProjection : IDisposable,
		ICoreProjection,
		ICoreProjectionForProcessingPhase,
		IHandle<CoreProjectionManagementMessage.GetState>,
		IHandle<CoreProjectionManagementMessage.GetResult>,
		IHandle<ProjectionManagementMessage.SlaveProjectionsStarted> {
		[Flags]
		private enum State : uint {
			Initial = 0x80000000,
			StartSlaveProjectionsRequested = 0x1,
			LoadStateRequested = 0x2,
			StateLoaded = 0x4,
			Subscribed = 0x8,
			Running = 0x10,
			Stopping = 0x40,
			Stopped = 0x80,
			FaultedStopping = 0x100,
			Faulted = 0x200,
			CompletingPhase = 0x400,
			PhaseCompleted = 0x800,
			Suspended = 0x900,
		}

		private readonly string _name;
		private readonly ProjectionVersion _version;

		private readonly IPublisher _publisher;
		private readonly IODispatcher _ioDispatcher;

		private readonly ProjectionProcessingStrategy _projectionProcessingStrategy;
		private readonly Guid _workerId;
		internal readonly Guid _projectionCorrelationId;
		private readonly IPublisher _inputQueue;
		private readonly IPrincipal _runAs;

		private readonly ILogger _logger;

		private State _state;

		private string _faultedReason;

		private readonly PartitionStateCache _partitionStateCache;
		private ICoreProjectionCheckpointManager _checkpointManager;
		private readonly ICoreProjectionCheckpointReader _checkpointReader;

		private bool _tickPending;

		private bool _startOnLoad;
		private bool _completed;

		private CheckpointSuggestedWorkItem _checkpointSuggestedWorkItem;
		private IProjectionProcessingPhase _projectionProcessingPhase;
		private readonly bool _stopOnEof;
		private readonly IProjectionProcessingPhase[] _projectionProcessingPhases;
		private readonly CoreProjectionCheckpointWriter _coreProjectionCheckpointWriter;
		private readonly bool _requiresRootPartition;
		private readonly Action<ProjectionStatistics> _enrichStatistics;

		private SlaveProjectionCommunicationChannels _slaveProjections;
		private int _statisticsSequentialNumber;
		private bool _disposed;

		private MasterCoreProjectionResponseReader _masterProjectionResponseReader;
		//NOTE: this is only for slave projections (TBD)


		public CoreProjection(
			ProjectionProcessingStrategy projectionProcessingStrategy,
			ProjectionVersion version,
			Guid projectionCorrelationId,
			IPublisher inputQueue,
			Guid workerId,
			IPrincipal runAs,
			IPublisher publisher,
			IODispatcher ioDispatcher,
			ReaderSubscriptionDispatcher subscriptionDispatcher,
			ILogger logger,
			ProjectionNamesBuilder namingBuilder,
			CoreProjectionCheckpointWriter coreProjectionCheckpointWriter,
			PartitionStateCache partitionStateCache,
			string effectiveProjectionName,
			ITimeProvider timeProvider) {
			if (publisher == null) throw new ArgumentNullException("publisher");
			if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");
			if (subscriptionDispatcher == null) throw new ArgumentNullException("subscriptionDispatcher");

			_projectionProcessingStrategy = projectionProcessingStrategy;
			_projectionCorrelationId = projectionCorrelationId;
			_inputQueue = inputQueue;
			_workerId = workerId;
			_runAs = runAs;
			_name = effectiveProjectionName;
			_version = version;
			_stopOnEof = projectionProcessingStrategy.GetStopOnEof();
			_logger = logger ?? LogManager.GetLoggerFor<CoreProjection>();
			_publisher = publisher;
			_ioDispatcher = ioDispatcher;
			_partitionStateCache = partitionStateCache;
			_requiresRootPartition = projectionProcessingStrategy.GetRequiresRootPartition();
			var useCheckpoints = projectionProcessingStrategy.GetUseCheckpoints();

			_coreProjectionCheckpointWriter = coreProjectionCheckpointWriter;

			_projectionProcessingPhases = projectionProcessingStrategy.CreateProcessingPhases(
				publisher,
				inputQueue,
				projectionCorrelationId,
				partitionStateCache,
				UpdateStatistics,
				this,
				namingBuilder,
				timeProvider,
				ioDispatcher,
				coreProjectionCheckpointWriter);


			//NOTE: currently assuming the first checkpoint manager to be able to load any state
			_checkpointReader = new CoreProjectionCheckpointReader(
				publisher,
				_projectionCorrelationId,
				ioDispatcher,
				namingBuilder.MakeCheckpointStreamName(),
				_version,
				useCheckpoints);
			_enrichStatistics = projectionProcessingStrategy.EnrichStatistics;
			GoToState(State.Initial);
		}

		private void BeginPhase(IProjectionProcessingPhase processingPhase, CheckpointTag startFrom,
			PartitionState rootPartitionState) {
			_projectionProcessingPhase = processingPhase;
			_projectionProcessingPhase.SetProjectionState(PhaseState.Starting);
			_checkpointManager = processingPhase.CheckpointManager;

			_projectionProcessingPhase.InitializeFromCheckpoint(startFrom);
			_checkpointManager.Start(startFrom, rootPartitionState);
		}

		private void UpdateStatistics() {
			if (_disposed)
				return;
			int sequentialNumber = _statisticsSequentialNumber++;
			var info = new ProjectionStatistics();
			GetStatistics(info);
			_publisher.Publish(
				new CoreProjectionStatusMessage.StatisticsReport(_projectionCorrelationId, info, sequentialNumber));
		}

		public void Start() {
			EnsureState(State.Initial);
			_startOnLoad = true;

			var slaveProjectionDefinitions = _projectionProcessingStrategy.GetSlaveProjections();
			if (slaveProjectionDefinitions != null) {
				GoToState(State.StartSlaveProjectionsRequested);
			} else {
				GoToState(State.LoadStateRequested);
			}
		}

		public void LoadStopped() {
			_startOnLoad = false;
			EnsureState(State.Initial);
			GoToState(State.LoadStateRequested);
		}

		public void Stop() {
			EnsureState(
				State.LoadStateRequested | State.StateLoaded | State.Subscribed | State.Running | State.PhaseCompleted
				| State.CompletingPhase);
			try {
				if (_state == State.LoadStateRequested || _state == State.PhaseCompleted)
					GoToState(State.Stopped);
				else
					GoToState(State.Stopping);
			} catch (Exception ex) {
				SetFaulted(ex);
			}
		}

		public void Kill() {
			if (_state != State.Stopped)
				GoToState(State.Stopped);
		}
		
		public bool Suspend() {
			if (_state == State.Stopped || _state == State.Suspended)
				return false;
			
			GoToState(State.Suspended);
			return true;
		}

		private void EnterSuspended() {
			EnsureUnsubscribed();
			_masterProjectionResponseReader?.Stop();
			_publisher.Publish(new CoreProjectionStatusMessage.Suspended(_projectionCorrelationId));
		}

		private void GetStatistics(ProjectionStatistics info) {
			_checkpointManager.GetStatistics(info);
			if (float.IsNaN(info.Progress) || float.IsNegativeInfinity(info.Progress)
			                               || float.IsPositiveInfinity(info.Progress)) {
				info.Progress = -2.0f;
			}

			info.Status = _state.EnumValueName() + info.Status;
			info.Name = _name;
			info.EffectiveName = _name;
			info.ProjectionId = _version.ProjectionId;
			info.Epoch = _version.Epoch;
			info.Version = _version.Version;
			info.StateReason = "";
			info.BufferedEvents = 0;
			info.PartitionsCached = _partitionStateCache.CachedItemCount;
			_enrichStatistics(info);
			if (_projectionProcessingPhase != null)
				_projectionProcessingPhase.GetStatistics(info);
		}

		public void CompletePhase() {
			if (_state != State.Running)
				return;
			if (!_stopOnEof)
				throw new InvalidOperationException("!_projectionConfig.StopOnEof");
			_completed = true;
			_checkpointManager.Progress(100.0f);
			GoToState(State.CompletingPhase);
		}

		public void Handle(CoreProjectionManagementMessage.GetState message) {
			if (_state == State.LoadStateRequested || _state == State.StateLoaded ||
			    _projectionProcessingPhase == null) {
				_publisher.Publish(
					new CoreProjectionStatusMessage.StateReport(
						message.CorrelationId, _projectionCorrelationId, message.Partition, state: null,
						position: null));
				return;
			}

			EnsureState(
				State.Running | State.Stopping | State.Stopped | State.FaultedStopping | State.Faulted
				| State.CompletingPhase | State.PhaseCompleted);

			_projectionProcessingPhase.Handle(message);
		}

		public void Handle(CoreProjectionManagementMessage.GetResult message) {
			if (_state == State.LoadStateRequested || _state == State.StateLoaded ||
			    _projectionProcessingPhase == null) {
				_publisher.Publish(
					new CoreProjectionStatusMessage.ResultReport(
						message.CorrelationId, _projectionCorrelationId, message.Partition, result: null,
						position: null));
				return;
			}

			EnsureState(
				State.Running | State.Stopping | State.Stopped | State.FaultedStopping | State.Faulted
				| State.CompletingPhase | State.PhaseCompleted);

			_projectionProcessingPhase.Handle(message);
		}

		public void Handle(CoreProjectionProcessingMessage.CheckpointCompleted message) {
			CheckpointCompleted(message.CheckpointTag);
		}

		public void Handle(CoreProjectionProcessingMessage.CheckpointLoaded message) {
			EnsureState(State.LoadStateRequested);
			try {
				var checkpointTag = message.CheckpointTag;
				var phase = checkpointTag == null ? 0 : checkpointTag.Phase;
				var projectionProcessingPhase = _projectionProcessingPhases[phase];
				if (checkpointTag == null)
					checkpointTag = projectionProcessingPhase.MakeZeroCheckpointTag();
				checkpointTag = projectionProcessingPhase.AdjustTag(checkpointTag);
				//TODO: initialize projection state here (test it)
				//TODO: write test to ensure projection state is correctly loaded from a checkpoint and posted back when enough empty records processed
				//TODO: handle errors
				_coreProjectionCheckpointWriter.StartFrom(checkpointTag, message.CheckpointEventNumber);

				PartitionState rootPartitionState = null;
				if (_requiresRootPartition) {
					rootPartitionState = PartitionState.Deserialize(message.CheckpointData, checkpointTag);
					_partitionStateCache.CacheAndLockPartitionState("", rootPartitionState, null);
				}

				BeginPhase(projectionProcessingPhase, checkpointTag, rootPartitionState);
				GoToState(State.StateLoaded);
				if (_startOnLoad) {
					if (_slaveProjections != null)
						_projectionProcessingPhase.AssignSlaves(_slaveProjections);
					_projectionProcessingPhase.Subscribe(checkpointTag, fromCheckpoint: true);
				} else
					GoToState(State.Stopped);
			} catch (Exception ex) {
				SetFaulted(ex);
			}
		}

		public void Handle(CoreProjectionProcessingMessage.PrerecordedEventsLoaded message) {
			EnsureState(State.StateLoaded);
			try {
				_projectionProcessingPhase.Handle(message);
			} catch (Exception ex) {
				SetFaulted(ex);
			}
		}

		public void Handle(CoreProjectionProcessingMessage.RestartRequested message) {
			_logger.Info(
				"Projection '{projection}'({projectionCorrelationId}) restart has been requested due to: '{reason}'",
				_name, _projectionCorrelationId,
				message.Reason);
			if (_state != State.Running) {
				SetFaulted(
					string.Format(
						"A concurrency violation was detected, but the projection is not running. Current state is: {0}.  The reason for the restart is: '{1}' ",
						_state, message.Reason));
				return;
			}

			//
			CompleteCheckpointSuggestedWorkItem();
			EnsureUnsubscribed();
			StopSlaveProjections();
			GoToState(State.Initial);
			Start();
		}

		public void Handle(CoreProjectionProcessingMessage.Failed message) {
			SetFaulted(message.Reason);
		}

		public void EnsureUnsubscribed() {
			if (_projectionProcessingPhase != null)
				_projectionProcessingPhase.EnsureUnsubscribed();
		}


		private void StopSlaveProjections() {
			if (_masterProjectionResponseReader != null)
				_masterProjectionResponseReader.Stop();
			//TODO: encapsulate into StopSlaveProjections message?
			var slaveProjections = _slaveProjections;
			if (slaveProjections != null) {
				_slaveProjections = null;
				foreach (var group in slaveProjections.Channels) {
					foreach (var channel in group.Value) {
						_publisher.Publish(
							new ProjectionManagementMessage.Command.Delete(
								new NoopEnvelope(), channel.ManagedProjectionName,
								ProjectionManagementMessage.RunAs.System, true, true, false));
					}
				}
			}
		}


		private void GoToState(State state) {
			if (_state == State.Suspended) {
				_logger.Debug($"Projection {_name} has been suspended for a subsystem restart. Cannot go to state {state}");
				return;
			}
//            _logger.Trace("CP: {projection} {stateFrom} => {stateTo}", _name, _state, state);
			var wasStopped = _state == State.Stopped || _state == State.Faulted || _state == State.PhaseCompleted;
			var wasStopping = _state == State.Stopping || _state == State.FaultedStopping
			                                           || _state == State.CompletingPhase;
			var wasStarting = _state == State.LoadStateRequested || _state == State.StateLoaded
			                                                     || _state == State.Subscribed;
			var wasStarted = _state == State.Subscribed || _state == State.Running || _state == State.Stopping
			                 || _state == State.FaultedStopping || _state == State.CompletingPhase;
			var wasRunning = _state == State.Running;
			var stateChanged = _state != state;
			_state = state; // set state before transition to allow further state change
			switch (state) {
				case State.Stopped:
				case State.Faulted:
				case State.PhaseCompleted:
					if (wasStarted && !wasStopped)
						_checkpointManager.Stopped();
					break;
				case State.Stopping:
				case State.FaultedStopping:
				case State.CompletingPhase:
					if (wasStarted && !wasStopping)
						_checkpointManager.Stopping();
					break;
			}


			if (_projectionProcessingPhase != null) // null while loading state
				switch (state) {
					case State.LoadStateRequested:
					case State.StateLoaded:
					case State.Subscribed:
						if (!wasStarting)
							_projectionProcessingPhase.SetProjectionState(PhaseState.Starting);
						break;
					case State.Running:
						if (!wasRunning)
							_projectionProcessingPhase.SetProjectionState(PhaseState.Running);
						break;
					case State.Faulted:
					case State.FaultedStopping:
						if (wasRunning)
							_projectionProcessingPhase.SetProjectionState(PhaseState.Stopped);
						break;
					case State.Stopped:
					case State.Stopping:
					case State.CompletingPhase:
					case State.PhaseCompleted:
						if (wasRunning)
							_projectionProcessingPhase.SetProjectionState(PhaseState.Stopped);
						break;
					default:
						_projectionProcessingPhase.SetProjectionState(PhaseState.Unknown);
						break;
				}
			switch (state) {
				case State.Initial:
					EnterInitial();
					break;
				case State.StartSlaveProjectionsRequested:
					EnterStartSlaveProjectionsRequested();
					break;
				case State.LoadStateRequested:
					EnterLoadStateRequested();
					break;
				case State.StateLoaded:
					EnterStateLoaded();
					break;
				case State.Subscribed:
					EnterSubscribed();
					break;
				case State.Running:
					EnterRunning();
					break;
				case State.Stopping:
					EnterStopping();
					break;
				case State.Stopped:
					EnterStopped();
					break;
				case State.FaultedStopping:
					EnterFaultedStopping();
					break;
				case State.Faulted:
					EnterFaulted();
					break;
				case State.CompletingPhase:
					EnterCompletingPhase();
					break;
				case State.PhaseCompleted:
					EnterPhaseCompleted();
					break;
				case State.Suspended:
					EnterSuspended();
					break;
				default:
					throw new Exception();
			}

			if (stateChanged)
				UpdateStatistics();
		}

		private void EnterInitial() {
			_completed = false;
			_partitionStateCache.Initialize();
			_projectionProcessingPhase = null;
			_checkpointManager = _projectionProcessingPhases[0].CheckpointManager;
			var emittedStreamsTracker = _projectionProcessingPhases[0].EmittedStreamsTracker;
			emittedStreamsTracker.Initialize();
			_checkpointManager.Initialize();
			_checkpointReader.Initialize();
			_tickPending = false;
			if (_requiresRootPartition)
				_partitionStateCache.CacheAndLockPartitionState("", new PartitionState("", null, CheckpointTag.Empty),
					null);
			// NOTE: this is to workaround exception in GetState requests submitted by client
		}

		private void EnterStartSlaveProjectionsRequested() {
			_masterProjectionResponseReader = new MasterCoreProjectionResponseReader(
				_publisher,
				_ioDispatcher,
				_workerId,
				_projectionCorrelationId);
			_masterProjectionResponseReader.Start();
			_publisher.Publish(
				new ProjectionManagementMessage.Command.StartSlaveProjections(
					new PublishEnvelope(_inputQueue),
					new ProjectionManagementMessage.RunAs(_runAs),
					_name,
					_projectionProcessingStrategy.GetSlaveProjections(),
					_workerId,
					_projectionCorrelationId));
		}

		private void EnterLoadStateRequested() {
			_checkpointReader.BeginLoadState();
		}

		private void EnterStateLoaded() {
		}

		private void EnterSubscribed() {
			if (_startOnLoad) {
				GoToState(State.Running);
			} else
				GoToState(State.Stopped);
		}

		private void EnterRunning() {
			try {
				_publisher.Publish(
					new CoreProjectionStatusMessage.Started(_projectionCorrelationId));
				_projectionProcessingPhase.ProcessEvent();
			} catch (Exception ex) {
				SetFaulted(ex);
			}
		}

		private void EnterStopping() {
			EnsureUnsubscribed();
		}

		private void EnterStopped() {
			EnsureUnsubscribed();
			StopSlaveProjections();
			_publisher.Publish(new CoreProjectionStatusMessage.Stopped(_projectionCorrelationId, _name, _completed));
		}

		private void EnterFaultedStopping() {
			EnsureUnsubscribed();
		}

		private void EnterFaulted() {
			EnsureUnsubscribed();
			StopSlaveProjections();
			_publisher.Publish(
				new CoreProjectionStatusMessage.Faulted(_projectionCorrelationId, _faultedReason));
		}

		private void EnterCompletingPhase() {
		}

		private void EnterPhaseCompleted() {
			var completedPhaseIndex = _checkpointManager.LastProcessedEventPosition.Phase;
			if (completedPhaseIndex == _projectionProcessingPhases.Length - 1) {
				Stop();
			} else {
				var nextPhase = _projectionProcessingPhases[completedPhaseIndex + 1];
				var nextPhaseZeroPosition = nextPhase.MakeZeroCheckpointTag();
				BeginPhase(nextPhase, nextPhaseZeroPosition, null);
				if (_slaveProjections != null)
					_projectionProcessingPhase.AssignSlaves(_slaveProjections);
				_projectionProcessingPhase.Subscribe(nextPhaseZeroPosition, fromCheckpoint: false);
			}
		}

		private void EnsureState(State expectedStates) {
			if ((_state & expectedStates) == 0) {
				throw new Exception(
					string.Format("Current state is {0}. Expected states are: {1}", _state, expectedStates));
			}
		}

		private void Tick() {
			// ignore any ticks received when not pending. this may happen when restart requested
			if (!_tickPending)
				return;
			// process messages in almost all states as we now ignore work items when processing
			if (_state == State.LoadStateRequested) {
				_tickPending = false;
				return;
			}

			EnsureState(
				State.Running | State.Stopping | State.Stopped | State.FaultedStopping | State.Faulted
				| State.CompletingPhase | State.PhaseCompleted);

			try {
				_tickPending = false;
				_projectionProcessingPhase.ProcessEvent();
			} catch (Exception ex) {
				SetFaulted(ex);
			}
		}


		public void Dispose() {
			_disposed = true;
			EnsureUnsubscribed();
			StopSlaveProjections();
			if (_projectionProcessingPhase != null)
				_projectionProcessingPhase.Dispose();
		}

		public void EnsureTickPending() {
			// ticks are requested when an async operation is completed or when an item is being processed
			// thus, the tick message is removed from the queue when it does not process any work item (and
			// it is renewed therefore)
			if (_tickPending)
				return;
			_tickPending = true;
			_publisher.Publish(new ProjectionCoreServiceMessage.CoreTick(Tick));
		}

		public void SetFaulted(Exception ex) {
			SetFaulted(ex.Message + "\r\n" + (ex.StackTrace ?? "").ToString());
		}

		public void SetFaulted(string reason) {
			if (_state != State.FaultedStopping && _state != State.Faulted)
				_faultedReason = reason;
			if (_state != State.Faulted)
				GoToState(State.Faulted);
		}

		public void SetFaulting(string reason) {
			if (_state != State.FaultedStopping && _state != State.Faulted) {
				_faultedReason = reason;
				GoToState(State.FaultedStopping);
			}
		}

		private void CheckpointCompleted(CheckpointTag lastCompletedCheckpointPosition) {
			CompleteCheckpointSuggestedWorkItem();
			// all emitted events caused by events before the checkpoint position have been written
			// unlock states, so the cache can be clean up as they can now be safely reloaded from the ES
			_partitionStateCache.Unlock(lastCompletedCheckpointPosition);

			switch (_state) {
				case State.Stopping:
					GoToState(State.Stopped);
					break;
				case State.FaultedStopping:
					GoToState(State.Faulted);
					break;
				case State.CompletingPhase:
					GoToState(State.PhaseCompleted);
					break;
			}
		}

		public void SetCurrentCheckpointSuggestedWorkItem(CheckpointSuggestedWorkItem checkpointSuggestedWorkItem) {
			if (_checkpointSuggestedWorkItem != null && checkpointSuggestedWorkItem != null)
				throw new InvalidOperationException("Checkpoint in progress");
			if (_checkpointSuggestedWorkItem == null && checkpointSuggestedWorkItem == null)
				throw new InvalidOperationException("No checkpoint in progress");
			_checkpointSuggestedWorkItem = checkpointSuggestedWorkItem;
		}

		private void CompleteCheckpointSuggestedWorkItem() {
			var workItem = _checkpointSuggestedWorkItem;
			if (workItem != null) {
				_checkpointSuggestedWorkItem = null;
				workItem.CheckpointCompleted();
				EnsureTickPending();
			}
		}


		public CheckpointTag LastProcessedEventPosition {
			get { return _checkpointManager.LastProcessedEventPosition; }
		}

		public void Subscribed() {
			GoToState(State.Subscribed);
		}

		public void Handle(ProjectionManagementMessage.SlaveProjectionsStarted message) {
			_slaveProjections = message.SlaveProjections;
			GoToState(State.LoadStateRequested);
		}
	}
}
