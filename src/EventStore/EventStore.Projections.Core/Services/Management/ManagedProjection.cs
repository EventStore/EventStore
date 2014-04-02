using System;
using System.Security.Principal;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Utils;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Projections.Core.Services.Management
{
    /// <summary>
    /// managed projection controls start/stop/create/update/delete lifecycle of the projection. 
    /// </summary>
    public class ManagedProjection : IDisposable
    {
        public class PersistedState
        {
            public string HandlerType { get; set; }
            public string Query { get; set; }
            public ProjectionMode Mode { get; set; }
            public bool Enabled { get; set; }
            public bool Deleted { get; set; }

            [Obsolete]
            public ProjectionSourceDefinition SourceDefintion
            {
                set { SourceDefinition = value; }
            }

            public ProjectionSourceDefinition SourceDefinition { get; set; }
            public bool? EmitEnabled { get; set; }
            public bool? CreateTempStreams { get; set; }
            public bool? CheckpointsDisabled { get; set; }
            public int? Epoch { get; set; }
            public int? Version { get; set; }
            public SerializedRunAs RunAs { get; set; }
        }

        private readonly IPublisher _inputQueue;
        private readonly IPublisher _output;

        private readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>
            _writeDispatcher;

        private readonly
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>
            _readDispatcher;

        private readonly
            RequestResponseDispatcher
                <CoreProjectionManagementMessage.GetState, CoreProjectionManagementMessage.StateReport>
            _getStateDispatcher;

        private readonly
            RequestResponseDispatcher
                <CoreProjectionManagementMessage.GetResult, CoreProjectionManagementMessage.ResultReport>
            _getResultDispatcher;


        private readonly ILogger _logger;
        private readonly ITimeProvider _timeProvider;
        private readonly ISingletonTimeoutScheduler _timeoutScheduler;
        private readonly IPublisher _coreQueue;
        private readonly Guid _workerId;
        private readonly Guid _id;
        private readonly int _projectionId;
        private readonly string _name;
        private readonly bool _enabledToRun;
        private ManagedProjectionState _state;
        private PersistedState _persistedState = new PersistedState();
        //private int _version;

        private string _faultedReason;
        private Action _onStopped;
        //private List<IEnvelope> _debugStateRequests;
        private ProjectionStatistics _lastReceivedStatistics;
        private Action _onPrepared;
        private Action _onStarted;
        private DateTime _lastAccessed;
        private int _lastWrittenVersion = -1;
        private IPrincipal _runAs;
        //TODO: slave (extract into derived class)

        private readonly bool _isSlave;
        private readonly Guid _slaveMasterWorkerId;
        private readonly Guid _slaveMasterCorrelationId;
        private Guid _slaveProjectionSubscriptionId;

        public ManagedProjection(
            Guid workerId,
            IPublisher coreQueue,
            Guid id,
            int projectionId,
            string name,
            bool enabledToRun,
            ILogger logger,
            RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> writeDispatcher,
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> readDispatcher,
            IPublisher inputQueue,
            IPublisher output,
            ITimeProvider timeProvider,
            ISingletonTimeoutScheduler timeoutScheduler = null,
            bool isSlave = false,
            Guid slaveMasterWorkerId = default(Guid),
            Guid slaveMasterCorrelationId = default(Guid))
        {
            if (coreQueue == null) throw new ArgumentNullException("coreQueue");
            if (id == Guid.Empty) throw new ArgumentException("id");
            if (name == null) throw new ArgumentNullException("name");
            if (output == null) throw new ArgumentNullException("output");
            if (name == "") throw new ArgumentException("name");
            _coreQueue = coreQueue;
            _id = id;
            _projectionId = projectionId;
            _name = name;
            _enabledToRun = enabledToRun;
            _logger = logger;
            _writeDispatcher = writeDispatcher;
            _readDispatcher = readDispatcher;
            _inputQueue = inputQueue;
            _output = output;
            _timeProvider = timeProvider;
            _timeoutScheduler = timeoutScheduler;
            _isSlave = isSlave;
            _slaveMasterWorkerId = slaveMasterWorkerId;
            _slaveMasterCorrelationId = slaveMasterCorrelationId;
            _getStateDispatcher =
                new RequestResponseDispatcher
                    <CoreProjectionManagementMessage.GetState, CoreProjectionManagementMessage.StateReport>(
                    coreQueue,
                    v => v.CorrelationId,
                    v => v.CorrelationId,
                    new PublishEnvelope(_inputQueue));
            _getResultDispatcher =
                new RequestResponseDispatcher
                    <CoreProjectionManagementMessage.GetResult, CoreProjectionManagementMessage.ResultReport>(
                    coreQueue,
                    v => v.CorrelationId,
                    v => v.CorrelationId,
                    new PublishEnvelope(_inputQueue));
            _lastAccessed = _timeProvider.Now;
        }

        private string HandlerType
        {
            get { return _persistedState.HandlerType; }
        }

        private string Query
        {
            get { return _persistedState.Query; }
        }

        private ProjectionMode Mode
        {
            get { return _persistedState.Mode; }
        }

        private bool Enabled
        {
            get { return _persistedState.Enabled; }
            set { _persistedState.Enabled = value; }
        }

        public bool Deleted
        {
            get { return _persistedState.Deleted; }
            private set { _persistedState.Deleted = value; }
        }

        //TODO: remove property. pass value back to completion routine
        public Guid SlaveProjectionSubscriptionId
        {
            get { return _slaveProjectionSubscriptionId; }
        }

        public Guid Id
        {
            get { return _id; }
        }

        public void Dispose()
        {
            DisposeCoreProjection();
        }

        public ProjectionMode GetMode()
        {
            return Mode;
        }

        public ProjectionStatistics GetStatistics()
        {
            ProjectionStatistics status;
            if (_lastReceivedStatistics == null)
            {
                status = new ProjectionStatistics
                    {
                        Name = _name,
                        Epoch = -1,
                        Version = -1,
                        Mode = GetMode(),
                        Status = _state.EnumValueName(),
                        MasterStatus = _state
                    };
            }
            else
            {
                status = _lastReceivedStatistics.Clone();
                status.Mode = GetMode();
                status.Name = _name;
                var enabledSuffix = ((_state == ManagedProjectionState.Stopped || _state == ManagedProjectionState.Faulted) && Enabled ? " (Enabled)" : "");
                status.Status = (status.Status == "Stopped" && _state == ManagedProjectionState.Completed
                                    ? _state.EnumValueName()
                                    : (!status.Status.StartsWith(_state.EnumValueName())
                                           ? _state.EnumValueName() + "/" + status.Status
                                           : status.Status)) + enabledSuffix;
                status.MasterStatus = _state;
            }
            if (_state == ManagedProjectionState.Faulted)
                status.StateReason = _faultedReason;
            status.Enabled = Enabled;
            return status;
        }

        public void Handle(ProjectionManagementMessage.GetQuery message)
        {
            _lastAccessed = _timeProvider.Now;
            if (!ProjectionManagementMessage.RunAs.ValidateRunAs(Mode, ReadWrite.Read, _runAs, message)) return;

            var emitEnabled = _persistedState.EmitEnabled ?? false;
            message.Envelope.ReplyWith(
                new ProjectionManagementMessage.ProjectionQuery(_name, Query, emitEnabled, _persistedState.SourceDefinition));
        }

        public void Handle(ProjectionManagementMessage.UpdateQuery message)
        {
            _lastAccessed = _timeProvider.Now;
            if (!ProjectionManagementMessage.RunAs.ValidateRunAs(Mode, ReadWrite.Write, _runAs, message)) return;

            Stop(() => DoUpdateQuery(message));
        }

        public void Handle(ProjectionManagementMessage.GetResult message)
        {
            _lastAccessed = _timeProvider.Now;
            if (_state >= ManagedProjectionState.Stopped)
            {
                _getResultDispatcher.Publish(
                    new CoreProjectionManagementMessage.GetResult(
                        new PublishEnvelope(_inputQueue), Guid.NewGuid(), Id, message.Partition, _workerId),
                    m =>
                    message.Envelope.ReplyWith(
                        new ProjectionManagementMessage.ProjectionResult(_name, m.Partition, m.Result, m.Position)));
            }
            else
            {
                message.Envelope.ReplyWith(
                    new ProjectionManagementMessage.ProjectionResult(
                        message.Name, message.Partition, "*** UNKNOWN ***", position: null));
            }
        }

        public void Handle(ProjectionManagementMessage.Disable message)
        {
            _lastAccessed = _timeProvider.Now;
            if (!ProjectionManagementMessage.RunAs.ValidateRunAs(Mode, ReadWrite.Write, _runAs, message)) return;
            Stop(() => DoDisable(message.Envelope, message.Name));
        }

        public void Handle(ProjectionManagementMessage.Abort message)
        {
            _lastAccessed = _timeProvider.Now;
            if (!ProjectionManagementMessage.RunAs.ValidateRunAs(Mode, ReadWrite.Write, _runAs, message)) return;
            Abort(() => DoDisable(message.Envelope, message.Name));
        }

        public void Handle(ProjectionManagementMessage.Enable message)
        {
            _lastAccessed = _timeProvider.Now;
            if (!ProjectionManagementMessage.RunAs.ValidateRunAs(Mode, ReadWrite.Write, _runAs, message)) return;
            if (Enabled
                && !(_state == ManagedProjectionState.Completed || _state == ManagedProjectionState.Faulted
                     || _state == ManagedProjectionState.Loaded || _state == ManagedProjectionState.Prepared
                     || _state == ManagedProjectionState.Stopped))
            {
                message.Envelope.ReplyWith(
                    new ProjectionManagementMessage.OperationFailed("Invalid state"));
                return;
            }
            if (!Enabled)
                Enable();
            Action completed = () =>
                {
                    if (_state == ManagedProjectionState.Prepared)
                        StartOrLoadStopped(() => message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(message.Name)));
                    else
                        message.Envelope.ReplyWith(
                            new ProjectionManagementMessage.Updated(message.Name));
                };
            UpdateProjectionVersion();
            Prepare(() => BeginWrite(completed));
        }

        public void Handle(ProjectionManagementMessage.SetRunAs message)
        {
            _lastAccessed = _timeProvider.Now;
            if (
                !ProjectionManagementMessage.RunAs.ValidateRunAs(
                    Mode, ReadWrite.Write, _runAs, message,
                    message.Action == ProjectionManagementMessage.SetRunAs.SetRemove.Set)) return;


            Stop(
                () =>
                    {
                        UpdateProjectionVersion();
                        _persistedState.RunAs = message.Action == ProjectionManagementMessage.SetRunAs.SetRemove.Set
                                                    ? SerializePrincipal(message.RunAs)
                                                    : null;
                        _runAs = DeserializePrincipal(_persistedState.RunAs);

                        Prepare(
                            () => BeginWrite(
                                () =>
                                    {
                                        StartOrLoadStopped(() => { });
                                        message.Envelope.ReplyWith(
                                            new ProjectionManagementMessage.Updated(message.Name));
                                    }));
                    });
        }

        public void Handle(ProjectionManagementMessage.Reset message)
        {
            _lastAccessed = _timeProvider.Now;
            if (!ProjectionManagementMessage.RunAs.ValidateRunAs(Mode, ReadWrite.Write, _runAs, message)) return;
            Stop(
                () =>
                {
                    ResetProjection();
                    Prepare(
                            () =>
                            BeginWrite(
                                () =>
                                StartOrLoadStopped(
                                    () =>
                                        message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(message.Name)))));
                });
        }

        private void ResetProjection()
        {
            UpdateProjectionVersion(force: true);
            _persistedState.Epoch = _persistedState.Version;
        }

        public void Handle(ProjectionManagementMessage.Delete message)
        {
            _lastAccessed = _timeProvider.Now;
            if (!ProjectionManagementMessage.RunAs.ValidateRunAs(Mode, ReadWrite.Write, _runAs, message)) return;
            Stop(() => DoDelete(message));
        }

        public void Handle(CoreProjectionManagementMessage.Started message)
        {
            _state = ManagedProjectionState.Running;
            if (_onStarted != null)
            {
                var action = _onStarted;
                _onStarted = null;
                action();
            }
        }

        public void Handle(CoreProjectionManagementMessage.Stopped message)
        {
            _state = message.Completed ? ManagedProjectionState.Completed : ManagedProjectionState.Stopped;
            OnStoppedOrFaulted();
        }

        private void OnStoppedOrFaulted()
        {
            FireStoppedOrFaulted();
        }

        private void FireStoppedOrFaulted()
        {
            var stopCompleted = _onStopped;
            _onStopped = null;
            if (stopCompleted != null) stopCompleted();
        }

        public void Handle(CoreProjectionManagementMessage.Faulted message)
        {
            SetFaulted(message.FaultedReason);
            if (_state == ManagedProjectionState.Preparing)
            {
                // cannot prepare - thus we don't know source defintion
                _persistedState.SourceDefinition = null;
                OnPrepared();
            }
            OnStoppedOrFaulted();
        }

        public void Handle(CoreProjectionManagementMessage.Prepared message)
        {
            _persistedState.SourceDefinition = message.SourceDefinition;
            if (_state == ManagedProjectionState.Preparing)
            {
                _state = ManagedProjectionState.Prepared;
                OnPrepared();
            }
            else
            {
                _logger.Trace("Received prepared without being prepared");
            }
        }

        public void Handle(CoreProjectionManagementMessage.StateReport message)
        {
            _getStateDispatcher.Handle(message);
        }

        public void Handle(CoreProjectionManagementMessage.ResultReport message)
        {
            _getResultDispatcher.Handle(message);
        }

        public void Handle(CoreProjectionManagementMessage.StatisticsReport message)
        {
            _lastReceivedStatistics = message.Statistics;
        }

        public void Handle(ProjectionManagementMessage.Internal.CleanupExpired message)
        {
            //TODO: configurable expiration
            if (IsExpiredProjection())
            {
                if (_state == ManagedProjectionState.Creating)
                {
                    // NOTE: workaround for stop not working on creating state (just ignore them)
                    return;
                }
                Stop(
                    () =>
                        Handle(
                            new ProjectionManagementMessage.Delete(
                                new NoopEnvelope(), _name, ProjectionManagementMessage.RunAs.System, false, false)));
            }
        }

        private bool IsExpiredProjection()
        {
            return Mode == ProjectionMode.Transient && !_isSlave && _lastAccessed.AddMinutes(5) < _timeProvider.Now;
        }

        public void InitializeNew(Action completed, PersistedState persistedState)
        {
            LoadPersistedState(persistedState);
            UpdateProjectionVersion();
            Prepare(() => BeginWrite(() => StartOrLoadStopped(completed)));
        }

        public static SerializedRunAs SerializePrincipal(ProjectionManagementMessage.RunAs runAs)
        {
            if (runAs.Principal == null)
                return null; // anonymous
            if (runAs.Principal == SystemAccount.Principal)
                return new SerializedRunAs {Name = "$system"};

            var genericPrincipal = runAs.Principal as OpenGenericPrincipal;
            if (genericPrincipal == null)
                throw new ArgumentException(
                    "OpenGenericPrincipal is the only supported principal type in projections", "runAs");
            return new SerializedRunAs {Name = runAs.Principal.Identity.Name, Roles = genericPrincipal.Roles};
        }

        public void InitializeExisting(string name)
        {
            _state = ManagedProjectionState.Loading;
            BeginLoad(name);
        }

        private void BeginLoad(string name)
        {
            var corrId = Guid.NewGuid();
            _readDispatcher.Publish(
                new ClientMessage.ReadStreamEventsBackward(
                    corrId, corrId, _readDispatcher.Envelope, "$projections-" + name, -1, 1, 
                    resolveLinkTos: false, requireMaster: false, validationStreamVersion: null, user: SystemAccount.Principal), 
                LoadCompleted);
        }

        private void LoadCompleted(ClientMessage.ReadStreamEventsBackwardCompleted completed)
        {
            if (completed.Result == ReadStreamResult.Success && completed.Events.Length == 1)
            {
                byte[] state = completed.Events[0].Event.Data;
                var persistedState = state.ParseJson<PersistedState>();

                _lastWrittenVersion = completed.Events[0].Event.EventNumber;
                FixUpOldFormat(completed, persistedState);
                FixupOldProjectionModes(persistedState);
                FixUpOldProjectionRunAs(persistedState);

                LoadPersistedState(persistedState);
                //TODO: encapsulate this into managed projection
                _state = ManagedProjectionState.Loaded;
                if (Enabled && _enabledToRun)
                {
                    if (Mode >= ProjectionMode.Continuous)
                        Prepare(() => Start(() => { }));
                }
                else
                    CreatePrepared(() => LoadStopped(() => { }));
                return;
            }

            _state = ManagedProjectionState.Creating;

            _logger.Trace(
                "Projection manager did not find any projection configuration records in the {0} stream.  Projection stays in CREATING state",
                completed.EventStreamId);
        }

        private void FixUpOldProjectionRunAs(PersistedState persistedState)
        {
            if (persistedState.RunAs == null || string.IsNullOrEmpty(persistedState.RunAs.Name))
            {
                _runAs = SystemAccount.Principal;
                persistedState.RunAs = SerializePrincipal(ProjectionManagementMessage.RunAs.System);
            }
        }

        private void FixUpOldFormat(ClientMessage.ReadStreamEventsBackwardCompleted completed, PersistedState persistedState)
        {
            if (persistedState.Version == null)
            {
                persistedState.Version = completed.Events[0].Event.EventNumber;
                persistedState.Epoch = -1;
            }
            if (_lastWrittenVersion > persistedState.Version)
                persistedState.Version = _lastWrittenVersion;
        }

        private void FixupOldProjectionModes(PersistedState persistedState)
        {
            switch ((int) persistedState.Mode)
            {
                case 2: // old continuous
                    persistedState.Mode = ProjectionMode.Continuous;
                    break;
                case 3: // old persistent
                    persistedState.Mode = ProjectionMode.Continuous;
                    persistedState.EmitEnabled = persistedState.EmitEnabled ?? true;
                    break;
            }
        }

        private void LoadPersistedState(PersistedState persistedState)
        {
            var handlerType = persistedState.HandlerType;
            var query = persistedState.Query;

            if (handlerType == null) throw new ArgumentNullException("persistedState", "HandlerType");
            if (query == null) throw new ArgumentNullException("persistedState", "Query");
            if (handlerType == "") throw new ArgumentException("HandlerType", "persistedState");

            if (_state != ManagedProjectionState.Creating && _state != ManagedProjectionState.Loading)
                throw new InvalidOperationException("LoadPersistedState is now allowed in this state");

            _persistedState = persistedState;
            _runAs = DeserializePrincipal(persistedState.RunAs);
        }

        private IPrincipal DeserializePrincipal(SerializedRunAs runAs)
        {
            if (runAs == null)
                return null;
            if (runAs.Name == null)
                return null;
            if (runAs.Name == "$system") //TODO: make sure nobody else uses it
                return SystemAccount.Principal;
            return new OpenGenericPrincipal(new GenericIdentity(runAs.Name), runAs.Roles);
        }

        private void OnPrepared()
        {
            if (_onPrepared != null)
            {
                var action = _onPrepared;
                _onPrepared = null;
                action();
            }
        }

        private void BeginWrite(Action completed)
        {
            if (Mode == ProjectionMode.Transient)
            {
                //TODO: move to common completion procedure
                _lastWrittenVersion = _persistedState.Version ?? -1;
                completed();
                return;
            }
            var oldState = _state;
            _state = ManagedProjectionState.Writing;
            var managedProjectionSerializedState = _persistedState.ToJsonBytes();
            var eventStreamId = "$projections-" + _name;
            var corrId = Guid.NewGuid();
            _writeDispatcher.Publish(
                new ClientMessage.WriteEvents(
                    corrId, corrId, _writeDispatcher.Envelope, true, eventStreamId, ExpectedVersion.Any,
                    new Event(Guid.NewGuid(), "$ProjectionUpdated", true, managedProjectionSerializedState, Empty.ByteArray),
                    SystemAccount.Principal),
                m => WriteCompleted(m, oldState, completed, eventStreamId));
        }

        private void WriteCompleted(
            ClientMessage.WriteEventsCompleted message, ManagedProjectionState completedState, Action completed,
            string eventStreamId)
        {
            if (_state != ManagedProjectionState.Writing)
            {
                _logger.Error("Projection definition write completed in non writing state. ({0})", _name);
            }
            if (message.Result == OperationResult.Success)
            {
                _logger.Info("'{0}' projection source has been written", _name);
                var writtenEventNumber = message.FirstEventNumber;
                if (writtenEventNumber != (_persistedState.Version ?? writtenEventNumber))
                    throw new Exception("Projection version and event number mismatch");
                _lastWrittenVersion = (_persistedState.Version ?? writtenEventNumber);
                _state = completedState;
                if (completed != null) completed();
                return;
            }
            _logger.Info(
                "Projection '{0}' source has not been written to {1}. Error: {2}", _name, eventStreamId,
                Enum.GetName(typeof (OperationResult), message.Result));
            if (message.Result == OperationResult.CommitTimeout || message.Result == OperationResult.ForwardTimeout
                || message.Result == OperationResult.PrepareTimeout
                || message.Result == OperationResult.WrongExpectedVersion)
            {
                _logger.Info("Retrying write projection source for {0}", _name);
                BeginWrite(completed);
            }
            else
                throw new NotSupportedException("Unsupported error code received");
        }

        private void Prepare(Action onPrepared)
        {
            var config = CreateDefaultProjectionConfiguration();
            DisposeCoreProjection();
            BeginCreateAndPrepare(config, onPrepared);
        }

        private void CreatePrepared(Action onPrepared)
        {
            var config = CreateDefaultProjectionConfiguration();
            DisposeCoreProjection();
            BeginCreatePrepared(config, onPrepared);
        }

        private void Start(Action completed)
        {
            if (!Enabled)
                throw new InvalidOperationException("Projection is disabled");
            _onStopped = _onStarted = () =>
                {
                    _onStopped = null;
                    _onStarted = null;
                    if (completed != null)
                        completed();
                };
            _state = ManagedProjectionState.Starting;
            _coreQueue.Publish(new CoreProjectionManagementMessage.Start(Id, _workerId));
        }

        private void LoadStopped(Action onLoaded)
        {
            _onStopped = onLoaded;
            _state = ManagedProjectionState.LoadingState;
            _coreQueue.Publish(new CoreProjectionManagementMessage.LoadStopped(Id, _workerId));
        }

        private void DisposeCoreProjection()
        {
            _coreQueue.Publish(new CoreProjectionManagementMessage.Dispose(Id, _workerId));
        }

        /// <summary>
        /// Enables managed projection, but does not automatically start it
        /// </summary>
        private void Enable()
        {
            if (Enabled)
                throw new InvalidOperationException("Projection is not disabled");
            Enabled = true;
        }

        /// <summary>
        /// Disables managed projection, but does not automatically stop it
        /// </summary>
        private void Disable()
        {
            if (!Enabled)
                throw new InvalidOperationException("Projection is not enabled");
            Enabled = false;
        }

        private void Delete()
        {
            Deleted = true;
        }

        private void BeginCreateAndPrepare(
            ProjectionConfig config, Action onPrepared)
        {
            _onPrepared = _onStopped = () =>
            {
                _onStopped = null;
                _onPrepared = null;
                if (onPrepared != null)
                    onPrepared();
            };
            if (config == null) throw new ArgumentNullException("config");

            //TODO: which states are allowed here?
            if (_state >= ManagedProjectionState.Preparing)
            {
                DisposeCoreProjection();
                _state = ManagedProjectionState.Loaded;
            }

            //TODO: load configuration from the definition


            var createProjectionMessage = _isSlave
                ? (Message)
                    new CoreProjectionManagementMessage.CreateAndPrepareSlave(
                        Id,
                        _workerId,
                        _name,
                        new ProjectionVersion(_projectionId, _persistedState.Epoch ?? 0, _persistedState.Version ?? 0),
                        config,
                        _slaveMasterWorkerId,
                        _slaveMasterCorrelationId,
                        HandlerType,
                        Query)
                : new CoreProjectionManagementMessage.CreateAndPrepare(
                    Id,
                    _workerId, 
                    _name,
                    new ProjectionVersion(_projectionId, _persistedState.Epoch ?? 0, _persistedState.Version ?? 0),
                    config,
                    HandlerType,
                    Query);

            //note: set runnign before start as coreProjection.start() can respond with faulted
            _state = ManagedProjectionState.Preparing;
            _coreQueue.Publish(createProjectionMessage);
        }

        private void BeginCreatePrepared(ProjectionConfig config, Action onPrepared)
        {
            _onPrepared = onPrepared;
            if (config == null) throw new ArgumentNullException("config");

            //TODO: which states are allowed here?
            if (_state >= ManagedProjectionState.Preparing)
            {
                DisposeCoreProjection();
                _state = ManagedProjectionState.Loaded;
            }

            //TODO: load configuration from the definition

            if (_persistedState.SourceDefinition == null)
                throw new Exception(
                    "The projection cannot be loaded as stopped as it was stored in the old format.  Update the projection query text to force prepare");

            var createProjectionMessage = new CoreProjectionManagementMessage.CreatePrepared(
                Id,
                _workerId,
                _name,
                new ProjectionVersion(_projectionId, _persistedState.Epoch ?? 0, _persistedState.Version ?? 1),
                config,
                QuerySourcesDefinition.From(_persistedState.SourceDefinition),
                HandlerType,
                Query);

            //note: set running before start as coreProjection.start() can respond with faulted
            _state = ManagedProjectionState.Preparing;
            _coreQueue.Publish(createProjectionMessage);
        }

        private void Stop(Action completed)
        {
            switch (_state)
            {
                case ManagedProjectionState.Stopped:
                case ManagedProjectionState.Completed:
                case ManagedProjectionState.Faulted:
                case ManagedProjectionState.Loaded:
                    if (completed != null) completed();
                    return;
                case ManagedProjectionState.Loading:
                case ManagedProjectionState.Creating:
                    throw new InvalidOperationException(
                        string.Format(
                            "Cannot stop a projection in the '{0}' state",
                            Enum.GetName(typeof (ManagedProjectionState), _state)));
                case ManagedProjectionState.Stopping:
                    _onStopped += completed;
                    return;
                case ManagedProjectionState.Running:
                case ManagedProjectionState.Starting:
                    _state = ManagedProjectionState.Stopping;
                    _onStopped = completed;
                    _coreQueue.Publish(new CoreProjectionManagementMessage.Stop(Id, _workerId));
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        private void Abort(Action completed)
        {
            switch (_state)
            {
                case ManagedProjectionState.Stopped:
                case ManagedProjectionState.Completed:
                case ManagedProjectionState.Faulted:
                case ManagedProjectionState.Loaded:
                    if (completed != null) completed();
                    return;
                case ManagedProjectionState.Loading:
                case ManagedProjectionState.Creating:
                    throw new InvalidOperationException(
                        string.Format(
                            "Cannot stop a projection in the '{0}' state",
                            Enum.GetName(typeof (ManagedProjectionState), _state)));
                case ManagedProjectionState.Stopping:
                    _onStopped = completed;
                    _coreQueue.Publish(new CoreProjectionManagementMessage.Kill(Id, _workerId));
                    return;
                case ManagedProjectionState.Running:
                case ManagedProjectionState.Starting:
                    _state = ManagedProjectionState.Stopping;
                    _onStopped = completed;
                    _coreQueue.Publish(new CoreProjectionManagementMessage.Kill(Id, _workerId));
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        private void SetFaulted(string reason, Exception ex = null)
        {
            if (ex != null)
                _logger.ErrorException(ex, "The '{0}' projection faulted due to '{1}'", _name, reason);
            else
                _logger.Error("The '{0}' projection faulted due to '{1}'", _name, reason);
            _state = ManagedProjectionState.Faulted;
            _faultedReason = reason;
        }

        private ProjectionConfig CreateDefaultProjectionConfiguration()
        {
            var checkpointsEnabled = _persistedState.CheckpointsDisabled != true;
            var checkpointHandledThreshold = checkpointsEnabled ? 4000 : 0;
            var checkpointUnhandledBytesThreshold = checkpointsEnabled ? 10*1000*1000 : 0;
            var pendingEventsThreshold = 5000;
            var maxWriteBatchLength = 500;
            var emitEventEnabled = _persistedState.EmitEnabled == true;
            var createTempStreams = _persistedState.CreateTempStreams == true;
            var stopOnEof = _persistedState.Mode <= ProjectionMode.OneTime;

            var projectionConfig = new ProjectionConfig(
                _runAs,
                checkpointHandledThreshold,
                checkpointUnhandledBytesThreshold,
                pendingEventsThreshold,
                maxWriteBatchLength,
                emitEventEnabled,
                checkpointsEnabled,
                createTempStreams,
                stopOnEof,
                isSlaveProjection: false);
            return projectionConfig;
        }

        private void StartOrLoadStopped(Action completed)
        {
            if (_state == ManagedProjectionState.Prepared || _state == ManagedProjectionState.Writing)
            {
                if (Enabled && _enabledToRun)
                    Start(completed);
                else
                    LoadStopped(completed);
            }
            else if (completed != null)
                completed();
        }


        private void DoUpdateQuery(ProjectionManagementMessage.UpdateQuery message)
        {
            _persistedState.HandlerType = message.HandlerType ?? HandlerType;
            _persistedState.Query = message.Query;
            _persistedState.EmitEnabled = message.EmitEnabled ?? _persistedState.EmitEnabled;
            if (_state == ManagedProjectionState.Completed)
            {
                ResetProjection();
            }
            Action completed = () =>
                {
                    StartOrLoadStopped(() => { });
                    message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(message.Name));
                };
            UpdateProjectionVersion();
            Prepare(() => BeginWrite(completed));
        }

        private void DoDisable(IEnvelope envelope, string name)
        {
            if (!Enabled)
            {
                envelope.ReplyWith(new ProjectionManagementMessage.OperationFailed("Not enabled"));
                return;
            }
            Disable();
            Action completed = () => envelope.ReplyWith(new ProjectionManagementMessage.Updated(name));
            UpdateProjectionVersion();
            if (Enabled)
                Prepare(() => BeginWrite(completed));
            else
                BeginWrite(completed);
        }

        private void DoDelete(ProjectionManagementMessage.Delete message)
        {
            if (Enabled)
                Disable();
            Delete();
            Action completed = () =>
                {
                    message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(_name));
                    DisposeCoreProjection();
                    _output.Publish(new ProjectionManagementMessage.Internal.Deleted(_name, Id));
                };
            UpdateProjectionVersion();
            if (Enabled)
                Prepare(() => BeginWrite(completed));
            else
                BeginWrite(completed);
        }

        private void UpdateProjectionVersion(bool force = false)
        {
            if (_lastWrittenVersion == _persistedState.Version)
                _persistedState.Version++;
            else if (force)
                throw new ApplicationException("Internal error: projection definition must be saved before forced updating version");
        }

        public void Handle(ProjectionManagementMessage.GetState message)
        {
            _lastAccessed = _timeProvider.Now;
            if (_state >= ManagedProjectionState.Stopped)
            {
                _getStateDispatcher.Publish(
                    new CoreProjectionManagementMessage.GetState(
                        new PublishEnvelope(_inputQueue), Guid.NewGuid(), Id, message.Partition, _workerId),
                    m =>
                    message.Envelope.ReplyWith(
                        new ProjectionManagementMessage.ProjectionState(_name, m.Partition, m.State, m.Position)));
            }
            else
            {
                message.Envelope.ReplyWith(
                    new ProjectionManagementMessage.ProjectionState(
                        message.Name, message.Partition, "*** UNKNOWN ***", position: null));
            }
        }

        public void Handle(CoreProjectionManagementMessage.SlaveProjectionReaderAssigned message)
        {
            _slaveProjectionSubscriptionId = message.SubscriptionId;
        }
    }

    public class SerializedRunAs
    {
        public string Name { get; set; }
        public string[] Roles { get; set; }
    }
}
