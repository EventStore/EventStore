using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;
using EventStore.Core.Helpers;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Standard;
using EventStore.Projections.Core.Common;

namespace EventStore.Projections.Core.Services.Management {
	public class ProjectionManager
		: IDisposable,
			IHandle<SystemMessage.StateChangeMessage>,
			IHandle<SystemMessage.SystemCoreReady>,
			IHandle<SystemMessage.EpochWritten>,
			IHandle<ClientMessage.ReadStreamEventsBackwardCompleted>,
			IHandle<ClientMessage.ReadStreamEventsForwardCompleted>,
			IHandle<ClientMessage.WriteEventsCompleted>,
			IHandle<ClientMessage.DeleteStreamCompleted>,
			IHandle<ProjectionManagementMessage.Command.Post>,
			IHandle<ProjectionManagementMessage.Command.UpdateQuery>,
			IHandle<ProjectionManagementMessage.Command.GetQuery>,
			IHandle<ProjectionManagementMessage.Command.Delete>,
			IHandle<ProjectionManagementMessage.Command.GetStatistics>,
			IHandle<ProjectionManagementMessage.Command.GetState>,
			IHandle<ProjectionManagementMessage.Command.GetResult>,
			IHandle<ProjectionManagementMessage.Command.Disable>,
			IHandle<ProjectionManagementMessage.Command.Enable>,
			IHandle<ProjectionManagementMessage.Command.Abort>,
			IHandle<ProjectionManagementMessage.Command.SetRunAs>,
			IHandle<ProjectionManagementMessage.Command.Reset>,
			IHandle<ProjectionManagementMessage.Command.StartSlaveProjections>,
			IHandle<ProjectionManagementMessage.Command.GetConfig>,
			IHandle<ProjectionManagementMessage.Command.UpdateConfig>,
			IHandle<ProjectionManagementMessage.Internal.CleanupExpired>,
			IHandle<ProjectionManagementMessage.Internal.Deleted>,
			IHandle<CoreProjectionStatusMessage.Started>,
			IHandle<CoreProjectionStatusMessage.Stopped>,
			IHandle<CoreProjectionStatusMessage.Faulted>,
			IHandle<CoreProjectionStatusMessage.Prepared>,
			IHandle<CoreProjectionStatusMessage.StateReport>,
			IHandle<CoreProjectionStatusMessage.ResultReport>,
			IHandle<CoreProjectionStatusMessage.StatisticsReport>,
			IHandle<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>,
			IHandle<ProjectionManagementMessage.RegisterSystemProjection>,
			IHandle<CoreProjectionStatusMessage.ProjectionWorkerStarted>,
			IHandle<ProjectionManagementMessage.ReaderReady> {
		public const int ProjectionQueryId = -2;
		public const int ProjectionCreationRetryCount = 1;

		private readonly ILogger _logger = LogManager.GetLoggerFor<ProjectionManager>();

		private readonly IPublisher _inputQueue;
		private readonly IPublisher _publisher;
		private readonly Tuple<Guid, IPublisher>[] _queues;
		private readonly Guid[] _workers;
		private readonly TimeSpan _projectionsQueryExpiry;

		private readonly ITimeProvider _timeProvider;
		private readonly ProjectionType _runProjections;
		private readonly bool _initializeSystemProjections;
		private readonly Dictionary<string, ManagedProjection> _projections;
		private readonly Dictionary<Guid, string> _projectionsMap;

		private readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>
			_writeDispatcher;

		private readonly RequestResponseDispatcher<ClientMessage.DeleteStream, ClientMessage.DeleteStreamCompleted>
			_streamDispatcher;

		private readonly
			RequestResponseDispatcher
			<ClientMessage.ReadStreamEventsForward, ClientMessage.ReadStreamEventsForwardCompleted>
			_readForwardDispatcher;

		private readonly
			RequestResponseDispatcher
			<ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>
			_readDispatcher;

		private int _readEventsBatchSize = 100;

		private int _lastUsedQueue = 0;
		private bool _started;
		private bool _projectionsStarted;
		private readonly PublishEnvelope _publishEnvelope;

		private readonly
			RequestResponseDispatcher<CoreProjectionManagementMessage.GetState, CoreProjectionStatusMessage.StateReport>
			_getStateDispatcher;

		private readonly
			RequestResponseDispatcher
			<CoreProjectionManagementMessage.GetResult, CoreProjectionStatusMessage.ResultReport>
			_getResultDispatcher;

		private readonly IODispatcher _ioDispatcher;

		public ProjectionManager(
			IPublisher inputQueue,
			IPublisher publisher,
			IDictionary<Guid, IPublisher> queueMap,
			ITimeProvider timeProvider,
			ProjectionType runProjections,
			IODispatcher ioDispatcher,
			TimeSpan projectionQueryExpiry,
			bool initializeSystemProjections = true) {
			if (inputQueue == null) throw new ArgumentNullException("inputQueue");
			if (publisher == null) throw new ArgumentNullException("publisher");
			if (queueMap == null) throw new ArgumentNullException("queueMap");
			if (queueMap.Count == 0) throw new ArgumentException("At least one queue is required", "queueMap");

			_inputQueue = inputQueue;
			_publisher = publisher;
			_queues = queueMap.Select(v => Tuple.Create(v.Key, v.Value)).ToArray();
			_workers = _queues.Select(v => v.Item1).ToArray();

			_timeProvider = timeProvider;
			_runProjections = runProjections;
			_initializeSystemProjections = initializeSystemProjections;
			_ioDispatcher = ioDispatcher;
			_projectionsQueryExpiry = projectionQueryExpiry;

			_writeDispatcher =
				new RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>(
					publisher,
					v => v.CorrelationId,
					v => v.CorrelationId,
					new PublishEnvelope(_inputQueue));
			_readDispatcher =
				new RequestResponseDispatcher
					<ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>(
						publisher,
						v => v.CorrelationId,
						v => v.CorrelationId,
						new PublishEnvelope(_inputQueue));
			_readForwardDispatcher =
				new RequestResponseDispatcher
					<ClientMessage.ReadStreamEventsForward, ClientMessage.ReadStreamEventsForwardCompleted>(
						publisher,
						v => v.CorrelationId,
						v => v.CorrelationId,
						new PublishEnvelope(_inputQueue));
			_streamDispatcher =
				new RequestResponseDispatcher<ClientMessage.DeleteStream, ClientMessage.DeleteStreamCompleted>(
					publisher,
					v => v.CorrelationId,
					v => v.CorrelationId,
					new PublishEnvelope(_inputQueue));

			_projections = new Dictionary<string, ManagedProjection>();
			_projectionsMap = new Dictionary<Guid, string>();
			_publishEnvelope = new PublishEnvelope(_inputQueue, crossThread: true);
			_getStateDispatcher =
				new RequestResponseDispatcher
					<CoreProjectionManagementMessage.GetState, CoreProjectionStatusMessage.StateReport>(
						_publisher,
						v => v.CorrelationId,
						v => v.CorrelationId,
						new PublishEnvelope(_inputQueue));
			_getResultDispatcher =
				new RequestResponseDispatcher
					<CoreProjectionManagementMessage.GetResult, CoreProjectionStatusMessage.ResultReport>(
						_publisher,
						v => v.CorrelationId,
						v => v.CorrelationId,
						new PublishEnvelope(_inputQueue));
		}

		private void Start() {
			if (_started)
				throw new InvalidOperationException();
			_started = true;
			_publisher.Publish(new ProjectionManagementMessage.Starting(_epochId));
		}

		public void Handle(ProjectionManagementMessage.ReaderReady message) {
			if (_runProjections >= ProjectionType.System)
				StartExistingProjections(
					() => {
						_projectionsStarted = true;
						ScheduleExpire();
						_publisher.Publish(new SystemMessage.SubSystemInitialized("Projections"));
					});
		}


		private void ScheduleExpire() {
			if (!_projectionsStarted)
				return;
			_publisher.Publish(
				TimerMessage.Schedule.Create(
					TimeSpan.FromSeconds(60),
					_publishEnvelope,
					new ProjectionManagementMessage.Internal.CleanupExpired()));
		}

		private void Stop() {
			_started = false;
			_projectionsStarted = false;

			_writeDispatcher.CancelAll();
			_readDispatcher.CancelAll();

			_projections.Clear();
			_projectionsMap.Clear();
		}

		public void Handle(ProjectionManagementMessage.Command.Post message) {
			if (!_projectionsStarted)
				return;

			if (
				!ProjectionManagementMessage.RunAs.ValidateRunAs(
					message.Mode,
					ReadWrite.Write,
					null,
					message,
					replace: message.EnableRunAs)) return;

			if (message.Name == null) {
				message.Envelope.ReplyWith(
					new ProjectionManagementMessage.OperationFailed("Projection name is required"));
			} else {
				if (_projections.ContainsKey(message.Name)) {
					message.Envelope.ReplyWith(
						new ProjectionManagementMessage.Conflict("Duplicate projection name: " + message.Name));
				} else {
					PostNewProjection(message, message.Envelope);
				}
			}
		}

		public void Handle(ProjectionManagementMessage.Command.Delete message) {
			if (!_projectionsStarted)
				return;
			var projection = GetProjection(message.Name);
			if (projection == null)
				message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
			if (IsSystemProjection(message.Name)) {
				message.Envelope.ReplyWith(
					new ProjectionManagementMessage.OperationFailed(
						"We currently don't allow for the deletion of System Projections."));
				return;
			} else {
				if (!ProjectionManagementMessage.RunAs.ValidateRunAs(projection.Mode, ReadWrite.Write, projection.RunAs,
					message)) return;
				try {
					projection.Handle(message);
				} catch (InvalidOperationException ex) {
					message.Envelope.ReplyWith(new ProjectionManagementMessage.OperationFailed(ex.Message));
					return;
				}
			}
		}

		private bool IsSystemProjection(string name) {
			return name == ProjectionNamesBuilder.StandardProjections.EventByCategoryStandardProjection ||
			       name == ProjectionNamesBuilder.StandardProjections.EventByTypeStandardProjection ||
			       name == ProjectionNamesBuilder.StandardProjections.StreamByCategoryStandardProjection ||
			       name == ProjectionNamesBuilder.StandardProjections.StreamsStandardProjection ||
			       name == ProjectionNamesBuilder.StandardProjections.EventByCorrIdStandardProjection;
		}

		public void Handle(ProjectionManagementMessage.Command.GetQuery message) {
			if (!_projectionsStarted)
				return;
			var projection = GetProjection(message.Name);
			if (projection == null)
				message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
			else {
				if (!ProjectionManagementMessage.RunAs.ValidateRunAs(projection.Mode, ReadWrite.Read, projection.RunAs,
					message)) return;
				projection.Handle(message);
			}
		}

		public void Handle(ProjectionManagementMessage.Command.UpdateQuery message) {
			if (!_projectionsStarted)
				return;
			_logger.Info(
				"Updating '{projection}' projection source to '{source}' (Requested type is: '{type}')",
				message.Name,
				message.Query,
				message.HandlerType);
			var projection = GetProjection(message.Name);
			if (projection == null)
				message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
			else {
				if (!ProjectionManagementMessage.RunAs.ValidateRunAs(projection.Mode, ReadWrite.Write, projection.RunAs,
					message)) return;
				projection.Handle(message); // update query text
			}
		}

		public void Handle(ProjectionManagementMessage.Command.Disable message) {
			if (!_projectionsStarted)
				return;
			_logger.Info("Disabling '{projection}' projection", message.Name);

			var projection = GetProjection(message.Name);
			if (projection == null)
				message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
			else {
				if (!ProjectionManagementMessage.RunAs.ValidateRunAs(projection.Mode, ReadWrite.Write, projection.RunAs,
					message)) return;
				projection.Handle(message);
			}
		}

		public void Handle(ProjectionManagementMessage.Command.Enable message) {
			if (!_projectionsStarted)
				return;
			_logger.Info("Enabling '{projection}' projection", message.Name);

			var projection = GetProjection(message.Name);
			if (projection == null) {
				_logger.Error("DBG: PROJECTION *{projection}* NOT FOUND.", message.Name);
				message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
			} else {
				if (!ProjectionManagementMessage.RunAs.ValidateRunAs(projection.Mode, ReadWrite.Write, projection.RunAs,
					message)) return;
				projection.Handle(message);
			}
		}

		public void Handle(ProjectionManagementMessage.Command.Abort message) {
			if (!_projectionsStarted)
				return;
			_logger.Info("Aborting '{projection}' projection", message.Name);

			var projection = GetProjection(message.Name);
			if (projection == null)
				message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
			else {
				if (!ProjectionManagementMessage.RunAs.ValidateRunAs(projection.Mode, ReadWrite.Write, projection.RunAs,
					message)) return;
				projection.Handle(message);
			}
		}

		public void Handle(ProjectionManagementMessage.Command.SetRunAs message) {
			if (!_projectionsStarted)
				return;
			_logger.Info("Setting RunAs1 account for '{projection}' projection", message.Name);

			var projection = GetProjection(message.Name);
			if (projection == null) {
				_logger.Error("DBG: PROJECTION *{projection}* NOT FOUND.", message.Name);
				message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
			} else {
				if (
					!ProjectionManagementMessage.RunAs.ValidateRunAs(
						projection.Mode, ReadWrite.Write, projection.RunAs, message,
						message.Action == ProjectionManagementMessage.Command.SetRunAs.SetRemove.Set)) return;

				projection.Handle(message);
			}
		}

		public void Handle(ProjectionManagementMessage.Command.Reset message) {
			if (!_projectionsStarted)
				return;
			_logger.Info("Resetting '{projection}' projection", message.Name);

			var projection = GetProjection(message.Name);
			if (projection == null) {
				_logger.Error("DBG: PROJECTION *{projection}* NOT FOUND.", message.Name);
				message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
			} else {
				if (!ProjectionManagementMessage.RunAs.ValidateRunAs(projection.Mode, ReadWrite.Write, projection.RunAs,
					message)) return;
				projection.Handle(message);
			}
		}

		public void Handle(ProjectionManagementMessage.Command.GetStatistics message) {
			if (!_projectionsStarted)
				return;
			if (!string.IsNullOrEmpty(message.Name)) {
				var projection = GetProjection(message.Name);
				if (projection == null)
					message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
				else
					message.Envelope.ReplyWith(
						new ProjectionManagementMessage.Statistics(new[] {projection.GetStatistics()}));
			} else {
				var statuses = (from projectionNameValue in _projections
					let projection = projectionNameValue.Value
					where !projection.Deleted
					where
						message.Mode == null || message.Mode == projection.Mode
						                     || (message.Mode.GetValueOrDefault() == ProjectionMode.AllNonTransient
						                         && projection.Mode != ProjectionMode.Transient)
					let status = projection.GetStatistics()
					select status).ToArray();
				message.Envelope.ReplyWith(new ProjectionManagementMessage.Statistics(statuses));
			}
		}

		public void Handle(ProjectionManagementMessage.Command.GetState message) {
			if (!_projectionsStarted)
				return;
			var projection = GetProjection(message.Name);
			if (projection == null)
				message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
			else
				projection.Handle(message);
		}

		public void Handle(ProjectionManagementMessage.Command.GetResult message) {
			if (!_projectionsStarted)
				return;
			var projection = GetProjection(message.Name);
			if (projection == null)
				message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
			else
				projection.Handle(message);
		}

		public void Handle(ProjectionManagementMessage.Command.GetConfig message) {
			if (!_projectionsStarted)
				return;
			var projection = GetProjection(message.Name);
			if (projection == null)
				message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
			else {
				if (!ProjectionManagementMessage.RunAs.ValidateRunAs(projection.Mode, ReadWrite.Read, projection.RunAs,
					message)) return;
				projection.Handle(message);
			}
		}

		public void Handle(ProjectionManagementMessage.Command.UpdateConfig message) {
			if (!_projectionsStarted)
				return;
			var projection = GetProjection(message.Name);
			if (projection == null)
				message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
			else {
				if (!ProjectionManagementMessage.RunAs.ValidateRunAs(projection.Mode, ReadWrite.Read, projection.RunAs,
					message)) return;
				try {
					projection.Handle(message);
				} catch (InvalidOperationException ex) {
					message.Envelope.ReplyWith(new ProjectionManagementMessage.OperationFailed(ex.Message));
					return;
				}
			}
		}

		public void Handle(ProjectionManagementMessage.Internal.CleanupExpired message) {
			ScheduleExpire();
			CleanupExpired();
		}

		private void CleanupExpired() {
			foreach (var managedProjection in _projections.ToArray()) {
				managedProjection.Value.Handle(new ProjectionManagementMessage.Internal.CleanupExpired());
			}
		}

		public void Handle(CoreProjectionStatusMessage.Started message) {
			string name;
			if (_projectionsMap.TryGetValue(message.ProjectionId, out name)) {
				var projection = _projections[name];
				projection.Handle(message);
			}
		}

		public void Handle(CoreProjectionStatusMessage.Stopped message) {
			string name;
			if (_projectionsMap.TryGetValue(message.ProjectionId, out name)) {
				var projection = _projections[name];
				projection.Handle(message);
			}
		}

		public void Handle(CoreProjectionStatusMessage.Faulted message) {
			string name;
			if (_projectionsMap.TryGetValue(message.ProjectionId, out name)) {
				var projection = _projections[name];
				projection.Handle(message);
			}
		}

		public void Handle(CoreProjectionStatusMessage.Prepared message) {
			string name;
			if (_projectionsMap.TryGetValue(message.ProjectionId, out name)) {
				var projection = _projections[name];
				projection.Handle(message);
			}
		}

		public void Handle(CoreProjectionStatusMessage.StateReport message) {
			_getStateDispatcher.Handle(message);
		}

		public void Handle(CoreProjectionStatusMessage.ResultReport message) {
			_getResultDispatcher.Handle(message);
		}

		public void Handle(CoreProjectionStatusMessage.StatisticsReport message) {
			string name;
			if (_projectionsMap.TryGetValue(message.ProjectionId, out name)) {
				var projection = _projections[name];
				projection.Handle(message);
			}
		}

		public void Handle(CoreProjectionManagementMessage.SlaveProjectionReaderAssigned message) {
			Action<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned> action;
			if (_awaitingSlaveProjections.TryGetValue(message.ProjectionId, out action)) {
				action(message);
			}
		}

		public void Handle(ClientMessage.ReadStreamEventsBackwardCompleted message) {
			_readDispatcher.Handle(message);
		}

		public void Handle(ClientMessage.ReadStreamEventsForwardCompleted message) {
			_readForwardDispatcher.Handle(message);
		}

		public void Handle(ClientMessage.WriteEventsCompleted message) {
			_writeDispatcher.Handle(message);
		}

		public void Handle(ClientMessage.DeleteStreamCompleted message) {
			_streamDispatcher.Handle(message);
		}

		private VNodeState _currentState = VNodeState.Unknown;
		private bool _systemIsReady = false;
		private bool _ready = false;
		private Guid _epochId = Guid.Empty;

		public void Handle(SystemMessage.SystemCoreReady message) {
			_systemIsReady = true;
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
			if (_systemIsReady && _ready) {
				if (!_started) {
					_logger.Debug("PROJECTIONS: Starting Projections Manager. (Node State : {state})", _currentState);
					Start();
				}
			} else {
				if (_started) {
					_logger.Debug("PROJECTIONS: Stopping Projections Manager. (Node State : {state})", _currentState);
					Stop();
				}
			}
		}

		public void Handle(ProjectionManagementMessage.Internal.Deleted message) {
			var corrId = Guid.NewGuid();
			_writeDispatcher.Publish(
				new ClientMessage.WriteEvents(
					corrId,
					corrId,
					_writeDispatcher.Envelope,
					true,
					ProjectionNamesBuilder.ProjectionsRegistrationStream,
					ExpectedVersion.Any,
					new Event(
						Guid.NewGuid(),
						ProjectionEventTypes.ProjectionDeleted,
						false,
						Helper.UTF8NoBom.GetBytes(message.Name),
						Empty.ByteArray),
					SystemAccount.Principal),
				m => {
					_awaitingSlaveProjections.Remove(message.Id); // if any disconnected in error
					_projections.Remove(message.Name);
					_projectionsMap.Remove(message.Id);
				});
		}

		public void Handle(ProjectionManagementMessage.RegisterSystemProjection message) {
			if (!_projections.ContainsKey(message.Name)) {
				Handle(
					new ProjectionManagementMessage.Command.Post(
						new PublishEnvelope(_inputQueue),
						ProjectionMode.Continuous,
						message.Name,
						ProjectionManagementMessage.RunAs.System,
						message.Handler,
						message.Query,
						true,
						true,
						true,
						true,
						enableRunAs: true));
			}
		}

		public void Dispose() {
			foreach (var projection in _projections.Values)
				projection.Dispose();
			_projections.Clear();
		}

		private ManagedProjection GetProjection(string name) {
			ManagedProjection result;
			return _projections.TryGetValue(name, out result) ? result : null;
		}

		private void StartExistingProjections(Action completed) {
			var registeredProjections = new Dictionary<string, long>();
			ReadProjectionsList(ProjectionNamesBuilder.ProjectionsRegistrationStream, registeredProjections, completed);
		}

		private void ReadProjectionsList(string projectionsRegistrationStreamId,
			IDictionary<string, long> registeredProjections, Action completedAction, long from = 0) {
			_logger.Debug("PROJECTIONS: Reading Existing Projections from {stream}", projectionsRegistrationStreamId);
			var corrId = Guid.NewGuid();
			_readForwardDispatcher.Publish(
				new ClientMessage.ReadStreamEventsForward(
					corrId,
					corrId,
					_readDispatcher.Envelope,
					projectionsRegistrationStreamId,
					from,
					_readEventsBatchSize,
					resolveLinkTos: false,
					requireMaster: false,
					validationStreamVersion: null,
					user: SystemAccount.Principal),
				m => OnProjectionsListReadCompleted(m, registeredProjections, from, completedAction));
		}

		private void OnProjectionsListReadCompleted(ClientMessage.ReadStreamEventsForwardCompleted msg,
			IDictionary<string, long> registeredProjections, long requestedFrom, Action completedAction) {
			switch (msg.Result) {
				case ReadStreamResult.Success:
					foreach (var evnt in msg.Events) {
						var projectionId = evnt.Event.EventNumber;
						if (projectionId == 0)
							projectionId = Int32.MaxValue - 1;
						if (evnt.Event.EventType == ProjectionEventTypes.ProjectionsInitialized) {
							registeredProjections.Add(ProjectionEventTypes.ProjectionsInitialized, projectionId);
							continue;
						}

						var projectionName = Helper.UTF8NoBom.GetString(evnt.Event.Data);
						if (string.IsNullOrEmpty(projectionName)
						    || _projections.ContainsKey(projectionName)) {
							_logger.Warn(
								"PROJECTIONS: The following projection: {projection} has a duplicate registration event.",
								projectionName);
							continue;
						}

						if (evnt.Event.EventType == ProjectionEventTypes.ProjectionCreated) {
							if (registeredProjections.ContainsKey(projectionName)) {
								registeredProjections[projectionName] = projectionId;
								_logger.Warn(
									"PROJECTIONS: The following projection: {projection} has a duplicate created event. Using projection Id {projectionId}",
									projectionName, projectionId);
								continue;
							}

							registeredProjections.Add(projectionName, projectionId);
						} else if (evnt.Event.EventType == ProjectionEventTypes.ProjectionDeleted) {
							registeredProjections.Remove(projectionName);
						}
					}

					if (!msg.IsEndOfStream) {
						ReadProjectionsList(msg.EventStreamId, registeredProjections, completedAction,
							@from: msg.NextEventNumber);
						return;
					}

					break;
				case ReadStreamResult.StreamDeleted:
				case ReadStreamResult.Error:
				case ReadStreamResult.AccessDenied:
					_logger.Fatal(
						"There was an error reading the projections list due to {e}. Projections could not be loaded.",
						msg.Result);
					return;
			}

			StartRegisteredProjections(registeredProjections, completedAction);
		}

		private void StartRegisteredProjections(IDictionary<string, long> registeredProjections,
			Action completedAction) {
			if (!registeredProjections.Any()) {
				_logger.Debug("PROJECTIONS: No projections were found in {stream}, starting from empty stream",
					ProjectionNamesBuilder.ProjectionsRegistrationStream);
				WriteProjectionsInitialized(
					() => {
						completedAction();
						CreateSystemProjections();
					},
					Guid.NewGuid());
				return;
			}

			List<string> projections = registeredProjections
				.Where(x => x.Key != ProjectionEventTypes.ProjectionsInitialized)
				.Select(x => x.Key).ToList();

			_logger.Debug(
				"PROJECTIONS: Found the following projections in {stream}: " +
				(LogManager.StructuredLog ? "{@projections}" : "{projections}"),
				ProjectionNamesBuilder.ProjectionsRegistrationStream,
				LogManager.StructuredLog ? (object)projections : (object)String.Join(", ", projections));

			//create any missing system projections
			CreateSystemProjections(registeredProjections.Select(x => x.Key).ToList());

			foreach (var projectionRegistration in registeredProjections.Where(x =>
				x.Key != ProjectionEventTypes.ProjectionsInitialized)) {
				int queueIndex = GetNextWorkerIndex();
				var managedProjection = CreateManagedProjectionInstance(
					projectionRegistration.Key,
					projectionRegistration.Value,
					Guid.NewGuid(),
					_workers[queueIndex]);
				managedProjection.InitializeExisting(projectionRegistration.Key);
			}

			completedAction();
		}

		private bool IsProjectionEnabledToRunByMode(string projectionName) {
			return _runProjections >= ProjectionType.All
			       || _runProjections == ProjectionType.System && projectionName.StartsWith("$");
		}

		private void WriteProjectionsInitialized(Action action, Guid registrationEventId) {
			var corrId = Guid.NewGuid();
			_writeDispatcher.Publish(
				new ClientMessage.WriteEvents(
					corrId,
					corrId,
					_writeDispatcher.Envelope,
					true,
					ProjectionNamesBuilder.ProjectionsRegistrationStream,
					ExpectedVersion.NoStream,
					new Event(registrationEventId, ProjectionEventTypes.ProjectionsInitialized, false, Empty.ByteArray,
						Empty.ByteArray),
					SystemAccount.Principal),
				completed => WriteProjectionsInitializedCompleted(completed, registrationEventId, action));
		}

		private void WriteProjectionsInitializedCompleted(ClientMessage.WriteEventsCompleted completed,
			Guid registrationEventId, Action action) {
			switch (completed.Result) {
				case OperationResult.Success:
					action();
					break;
				case OperationResult.CommitTimeout:
				case OperationResult.ForwardTimeout:
				case OperationResult.PrepareTimeout:
					WriteProjectionsInitialized(action, registrationEventId);
					break;
				default:
					_logger.Fatal("Cannot initialize projections subsystem. Cannot write a fake projection");
					break;
			}
		}

		private void CreateSystemProjections() {
			CreateSystemProjections(new List<String>());
		}

		private void CreateSystemProjections(List<String> existingSystemProjections) {
			if (_initializeSystemProjections) {
				if (!existingSystemProjections.Contains(ProjectionNamesBuilder.StandardProjections
					.StreamsStandardProjection))
					CreateSystemProjection(
						ProjectionNamesBuilder.StandardProjections.StreamsStandardProjection,
						typeof(IndexStreams),
						"");

				if (!existingSystemProjections.Contains(ProjectionNamesBuilder.StandardProjections
					.StreamByCategoryStandardProjection))
					CreateSystemProjection(
						ProjectionNamesBuilder.StandardProjections.StreamByCategoryStandardProjection,
						typeof(CategorizeStreamByPath),
						"first\r\n-");

				if (!existingSystemProjections.Contains(ProjectionNamesBuilder.StandardProjections
					.EventByCategoryStandardProjection))
					CreateSystemProjection(
						ProjectionNamesBuilder.StandardProjections.EventByCategoryStandardProjection,
						typeof(CategorizeEventsByStreamPath),
						"first\r\n-");

				if (!existingSystemProjections.Contains(ProjectionNamesBuilder.StandardProjections
					.EventByTypeStandardProjection))
					CreateSystemProjection(
						ProjectionNamesBuilder.StandardProjections.EventByTypeStandardProjection,
						typeof(IndexEventsByEventType),
						"");

				if (!existingSystemProjections.Contains(ProjectionNamesBuilder.StandardProjections
					.EventByCorrIdStandardProjection))
					CreateSystemProjection(
						ProjectionNamesBuilder.StandardProjections.EventByCorrIdStandardProjection,
						typeof(ByCorrelationId),
						"{\"correlationIdProperty\":\"$correlationId\"}");
			}
		}

		private void CreateSystemProjection(string name, Type handlerType, string config) {
			IEnvelope envelope = new NoopEnvelope();

			var postMessage = new ProjectionManagementMessage.Command.Post(
				envelope,
				ProjectionMode.Continuous,
				name,
				ProjectionManagementMessage.RunAs.System,
				"native:" + handlerType.Namespace + "." + handlerType.Name,
				config,
				enabled: false,
				checkpointsEnabled: true,
				emitEnabled: true,
				trackEmittedStreams: false,
				enableRunAs: true);

			_publisher.Publish(postMessage);
		}

		private void CompletedReadingPossibleStream(
			ClientMessage.ReadStreamEventsBackwardCompleted completed,
			ProjectionManagementMessage.Command.Post message,
			IEnvelope replyEnvelope) {
			long version = -1;
			if (completed.Result == ReadStreamResult.Success) {
				version = completed.LastEventNumber + 1;
			}

			if (message.Mode >= ProjectionMode.OneTime) {
				var eventId = Guid.NewGuid();
				BeginWriteProjectionRegistration(
					message.Name,
					eventId,
					projectionId => { InitializeNewProjection(projectionId, message, version, replyEnvelope); },
					replyEnvelope, ProjectionCreationRetryCount);
			} else {
				InitializeNewProjection(ProjectionQueryId, message, version, replyEnvelope);
			}
		}

		private void InitializeNewProjection(long projectionId, ProjectionManagementMessage.Command.Post message,
			long version, IEnvelope replyEnvelope) {
			try {
				var initializer = new NewProjectionInitializer(
					projectionId,
					message.Name,
					message.Mode,
					message.HandlerType,
					message.Query,
					message.Enabled,
					message.EmitEnabled,
					message.CheckpointsEnabled,
					message.EnableRunAs,
					message.TrackEmittedStreams,
					message.RunAs,
					replyEnvelope);

				int queueIndex = GetNextWorkerIndex();
				initializer.CreateAndInitializeNewProjection(this, Guid.NewGuid(), _workers[queueIndex],
					version: version);
			} catch (Exception ex) {
				message.Envelope.ReplyWith(new ProjectionManagementMessage.OperationFailed(ex.Message));
			}
		}

		private void PostNewProjection(ProjectionManagementMessage.Command.Post message, IEnvelope replyEnvelope) {
			var corrId = Guid.NewGuid();
			_readDispatcher.Publish(
				new ClientMessage.ReadStreamEventsBackward(
					corrId,
					corrId,
					_readDispatcher.Envelope,
					ProjectionNamesBuilder.ProjectionsStreamPrefix + message.Name,
					0,
					_readEventsBatchSize,
					resolveLinkTos: false,
					requireMaster: false,
					validationStreamVersion: null,
					user: SystemAccount.Principal),
				m => CompletedReadingPossibleStream(m, message, replyEnvelope));
		}

		public class NewProjectionInitializer {
			private readonly long _projectionId;
			private readonly bool _enabled;
			private readonly string _handlerType;
			private readonly string _query;
			private readonly ProjectionMode _projectionMode;
			private readonly bool _emitEnabled;
			private readonly bool _checkpointsEnabled;
			private readonly bool _trackEmittedStreams;
			private readonly bool _enableRunAs;
			private readonly ProjectionManagementMessage.RunAs _runAs;
			private readonly IEnvelope _replyEnvelope;
			private readonly string _name;

			public NewProjectionInitializer(
				long projectionId,
				string name,
				ProjectionMode projectionMode,
				string handlerType,
				string query,
				bool enabled,
				bool emitEnabled,
				bool checkpointsEnabled,
				bool enableRunAs,
				bool trackEmittedStreams,
				ProjectionManagementMessage.RunAs runAs,
				IEnvelope replyEnvelope) {
				if (projectionMode >= ProjectionMode.Continuous && !checkpointsEnabled)
					throw new InvalidOperationException("Continuous mode requires checkpoints");

				if (emitEnabled && !checkpointsEnabled)
					throw new InvalidOperationException("Emit requires checkpoints");

				_projectionId = projectionId;
				_enabled = enabled;
				_handlerType = handlerType;
				_query = query;
				_projectionMode = projectionMode;
				_emitEnabled = emitEnabled;
				_checkpointsEnabled = checkpointsEnabled;
				_trackEmittedStreams = trackEmittedStreams;
				_enableRunAs = enableRunAs;
				_runAs = runAs;
				_replyEnvelope = replyEnvelope;
				_name = name;
			}

			public void CreateAndInitializeNewProjection(
				ProjectionManager projectionManager,
				Guid projectionCorrelationId,
				Guid workerId,
				bool isSlave = false,
				Guid slaveMasterWorkerId = default(Guid),
				Guid slaveMasterCorrelationId = default(Guid),
				long? version = -1) {
				var projection = projectionManager.CreateManagedProjectionInstance(
					_name,
					_projectionId,
					projectionCorrelationId,
					workerId,
					isSlave,
					slaveMasterWorkerId,
					slaveMasterCorrelationId);
				projection.InitializeNew(
					new ManagedProjection.PersistedState {
						Enabled = _enabled,
						HandlerType = _handlerType,
						Query = _query,
						Mode = _projectionMode,
						EmitEnabled = _emitEnabled,
						CheckpointsDisabled = !_checkpointsEnabled,
						TrackEmittedStreams = _trackEmittedStreams,
						CheckpointHandledThreshold = ProjectionConsts.CheckpointHandledThreshold,
						CheckpointAfterMs = (int)ProjectionConsts.CheckpointAfterMs.TotalMilliseconds,
						MaxAllowedWritesInFlight = ProjectionConsts.MaxAllowedWritesInFlight,
						Epoch = -1,
						Version = version,
						RunAs = _enableRunAs ? SerializedRunAs.SerializePrincipal(_runAs) : null
					},
					_replyEnvelope);
			}
		}

		private ManagedProjection CreateManagedProjectionInstance(
			string name,
			long projectionId,
			Guid projectionCorrelationId,
			Guid workerID,
			bool isSlave = false,
			Guid slaveMasterWorkerId = default(Guid),
			Guid slaveMasterCorrelationId = default(Guid)) {
			var enabledToRun = IsProjectionEnabledToRunByMode(name);
			var workerId = workerID;
			var managedProjectionInstance = new ManagedProjection(
				workerId,
				projectionCorrelationId,
				projectionId,
				name,
				enabledToRun,
				_logger,
				_streamDispatcher,
				_writeDispatcher,
				_readDispatcher,
				_publisher,
				_timeProvider,
				_getStateDispatcher,
				_getResultDispatcher,
				_ioDispatcher,
				_projectionsQueryExpiry,
				isSlave,
				slaveMasterWorkerId,
				slaveMasterCorrelationId);

			_projectionsMap.Add(projectionCorrelationId, name);
			_projections.Add(name, managedProjectionInstance);
			_logger.Debug("Adding projection {projectionCorrelationId}@{projection} to list", projectionCorrelationId,
				name);
			return managedProjectionInstance;
		}

		private int GetNextWorkerIndex() {
			if (_lastUsedQueue >= _workers.Length)
				_lastUsedQueue = 0;
			var queueIndex = _lastUsedQueue;
			_lastUsedQueue++;
			return queueIndex;
		}

		private void BeginWriteProjectionRegistration(string name, Guid eventId, Action<long> completed,
			IEnvelope envelope, int retryCount) {
			var corrId = Guid.NewGuid();
			_writeDispatcher.Publish(
				new ClientMessage.WriteEvents(
					corrId,
					corrId,
					_writeDispatcher.Envelope,
					true,
					ProjectionNamesBuilder.ProjectionsRegistrationStream,
					ExpectedVersion.Any,
					new Event(
						eventId,
						ProjectionEventTypes.ProjectionCreated,
						false,
						Helper.UTF8NoBom.GetBytes(name),
						Empty.ByteArray),
					SystemAccount.Principal),
				m => WriteProjectionRegistrationCompleted(m, eventId, completed, name,
					ProjectionNamesBuilder.ProjectionsRegistrationStream, envelope, retryCount));
		}

		private void WriteProjectionRegistrationCompleted(
			ClientMessage.WriteEventsCompleted message,
			Guid eventId,
			Action<long> completed,
			string name,
			string eventStreamId,
			IEnvelope replyEnvelope,
			int retryCount) {
			if (message.Result == OperationResult.Success) {
				completed?.Invoke(message.FirstEventNumber);
				return;
			}

			_logger.Info(
				"Projection '{projection}' registration has not been written to {stream}. Error: {e}",
				name,
				eventStreamId,
				Enum.GetName(typeof(OperationResult), message.Result));
			if (message.Result == OperationResult.CommitTimeout || message.Result == OperationResult.ForwardTimeout
			                                                    || message.Result == OperationResult.PrepareTimeout
			                                                    || message.Result ==
			                                                    OperationResult.WrongExpectedVersion) {
				if (retryCount > 0) {
					_logger.Info("Retrying write projection registration for {projection}", name);
					BeginWriteProjectionRegistration(name, eventId, completed, replyEnvelope, --retryCount);
					return;
				}
			}

			replyEnvelope.ReplyWith(new ProjectionManagementMessage.OperationFailed(
				string.Format(
					"The projection '{0}' could not be created because the registration could not be written due to {1}",
					name, message.Result)));
		}

		private readonly Dictionary<Guid, Action<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>>
			_awaitingSlaveProjections =
				new Dictionary<Guid, Action<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>>();


		public void Handle(ProjectionManagementMessage.Command.StartSlaveProjections message) {
			var result = new Dictionary<string, SlaveProjectionCommunicationChannel[]>();
			var counter = 0;
			foreach (var g in message.SlaveProjections.Definitions) {
				var @group = g;
				switch (g.RequestedNumber) {
					case SlaveProjectionDefinitions.SlaveProjectionRequestedNumber.One:
					case SlaveProjectionDefinitions.SlaveProjectionRequestedNumber.OnePerNode: {
						var resultArray = new SlaveProjectionCommunicationChannel[1];
						result.Add(g.Name, resultArray);
						counter++;
						int queueIndex = GetNextWorkerIndex();
						CINP(
							message,
							@group,
							resultArray,
							queueIndex,
							0,
							() => CheckSlaveProjectionsStarted(message, ref counter, result));
						break;
					}
					case SlaveProjectionDefinitions.SlaveProjectionRequestedNumber.OnePerThread: {
						var resultArray = new SlaveProjectionCommunicationChannel[_workers.Length];
						result.Add(g.Name, resultArray);

						for (int index = 0; index < _workers.Length; index++) {
							counter++;
							CINP(
								message,
								@group,
								resultArray,
								index,
								index,
								() => CheckSlaveProjectionsStarted(message, ref counter, result));
						}

						break;
					}
					default:
						throw new NotSupportedException();
				}
			}
		}

		private static void CheckSlaveProjectionsStarted(
			ProjectionManagementMessage.Command.StartSlaveProjections message,
			ref int counter,
			Dictionary<string, SlaveProjectionCommunicationChannel[]> result) {
			counter--;
			if (counter == 0)
				message.Envelope.ReplyWith(
					new ProjectionManagementMessage.SlaveProjectionsStarted(
						message.MasterCorrelationId,
						message.MasterWorkerId,
						new SlaveProjectionCommunicationChannels(result)));
		}

		private void CINP(
			ProjectionManagementMessage.Command.StartSlaveProjections message,
			SlaveProjectionDefinitions.Definition @group,
			SlaveProjectionCommunicationChannel[] resultArray,
			int queueIndex,
			int arrayIndex,
			Action completed) {
			var projectionCorrelationId = Guid.NewGuid();
			var slaveProjectionName = message.Name + "-" + @group.Name + "-" + queueIndex;
			_awaitingSlaveProjections.Add(
				projectionCorrelationId,
				assigned => {
					var queueWorkerId = _workers[queueIndex];

					resultArray[arrayIndex] = new SlaveProjectionCommunicationChannel(
						slaveProjectionName,
						queueWorkerId,
						assigned.SubscriptionId);
					completed();

					_awaitingSlaveProjections.Remove(projectionCorrelationId);
				});


			var initializer = new NewProjectionInitializer(
				ProjectionQueryId,
				slaveProjectionName,
				@group.Mode,
				@group.HandlerType,
				@group.Query,
				true,
				@group.EmitEnabled,
				@group.CheckpointsEnabled,
				@group.EnableRunAs,
				@group.TrackEmittedStreams,
				@group.RunAs1,
				replyEnvelope: null);

			initializer.CreateAndInitializeNewProjection(
				this,
				projectionCorrelationId,
				_workers[queueIndex],
				true,
				message.MasterWorkerId,
				message.MasterCorrelationId);
		}

		public void Handle(CoreProjectionStatusMessage.ProjectionWorkerStarted message) {
			RebalanceWork();
		}

		private void RebalanceWork() {
			//
		}
	}
}
