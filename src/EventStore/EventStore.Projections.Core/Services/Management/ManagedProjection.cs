using System;
using System.Diagnostics;
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
using EventStore.Projections.Core.Services.Management.ManagedProjectionStates;
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
        internal abstract class ManagedProjectionStateBase
        {
            protected readonly ManagedProjection _managedProjection;

            protected ManagedProjectionStateBase(ManagedProjection managedProjection)
            {
                _managedProjection = managedProjection;
            }

            private void Unexpected(string message)
            {
                _managedProjection.SetFaulted(message + "in " + this.GetType().Name);
            }

            protected void SetFaulted(string reason)
            {
                _managedProjection.SetFaulted(reason);
            }

            protected internal virtual void Started()
            {
                Unexpected("Unexpected 'STARTED' message");
            }

            protected internal virtual void Stopped(CoreProjectionStatusMessage.Stopped message)
            {
                Unexpected("Unexpected 'STOPPED' message");
            }

            protected internal virtual void Faulted(CoreProjectionStatusMessage.Faulted message)
            {
                Unexpected("Unexpected 'FAULTED' message"); 
            }

            protected internal virtual void Prepared(CoreProjectionStatusMessage.Prepared message)
            {
                Unexpected("Unexpected 'PREPARED' message");
            }
        }


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

        private readonly IPublisher _output;

        private readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>
            _writeDispatcher;

        private readonly
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>
            _readDispatcher;

        private readonly
            RequestResponseDispatcher
                <CoreProjectionManagementMessage.GetState, CoreProjectionStatusMessage.StateReport>
            _getStateDispatcher;

        private readonly
            RequestResponseDispatcher
                <CoreProjectionManagementMessage.GetResult, CoreProjectionStatusMessage.ResultReport>
            _getResultDispatcher;


        private readonly ILogger _logger;
        private readonly ITimeProvider _timeProvider;
        private readonly Guid _workerId;
        private readonly Guid _id;
        private readonly int _projectionId;
        private readonly string _name;
        private readonly bool _enabledToRun;
        private ManagedProjectionState _state;
        internal PersistedState _persistedState = new PersistedState();
        //private int _version;

        private string _faultedReason;
        //private List<IEnvelope> _debugStateRequests;
        private ProjectionStatistics _lastReceivedStatistics;
        private DateTime _lastAccessed;
        private int _lastWrittenVersion = -1;
        private IPrincipal _runAs;
        //TODO: slave (extract into derived class)

        private readonly bool _isSlave;
        private readonly Guid _slaveMasterWorkerId;
        private readonly Guid _slaveMasterCorrelationId;
        internal bool _prepared;
        internal bool _created;
        private bool _pendingPersistedState;

        private ManagedProjectionStateBase _stateHandler;
        private IEnvelope _lastReplyEnvelope;
        private bool _writing;

        public ManagedProjection(
            Guid workerId,
            Guid id,
            int projectionId,
            string name,
            bool enabledToRun,
            ILogger logger,
            RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> writeDispatcher,
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> readDispatcher,
            IPublisher output,
            ITimeProvider timeProvider,
            RequestResponseDispatcher<CoreProjectionManagementMessage.GetState, CoreProjectionStatusMessage.StateReport>
                getStateDispatcher,
            RequestResponseDispatcher
                <CoreProjectionManagementMessage.GetResult, CoreProjectionStatusMessage.ResultReport>
                getResultDispatcher,
            bool isSlave = false,
            Guid slaveMasterWorkerId = default(Guid),
            Guid slaveMasterCorrelationId = default(Guid))
        {
            if (id == Guid.Empty) throw new ArgumentException("id");
            if (name == null) throw new ArgumentNullException("name");
            if (output == null) throw new ArgumentNullException("output");
            if (getStateDispatcher == null) throw new ArgumentNullException("getStateDispatcher");
            if (getResultDispatcher == null) throw new ArgumentNullException("getResultDispatcher");
            if (name == "") throw new ArgumentException("name");
            _workerId = workerId;
            _id = id;
            _projectionId = projectionId;
            _name = name;
            _enabledToRun = enabledToRun;
            _logger = logger ?? LogManager.GetLoggerFor<ManagedProjection>();
            _writeDispatcher = writeDispatcher;
            _readDispatcher = readDispatcher;
            _output = output;
            _timeProvider = timeProvider;
            _isSlave = isSlave;
            _slaveMasterWorkerId = slaveMasterWorkerId;
            _slaveMasterCorrelationId = slaveMasterCorrelationId;
            _getStateDispatcher = getStateDispatcher;
            _getResultDispatcher = getResultDispatcher;
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

        public Guid Id
        {
            get { return _id; }
        }

        internal void SetState(ManagedProjectionState value)
        {
//            _logger.Trace("MP: {0} {1} => {2}", _name, _state, value);
            _state = value;
            switch (value)
            {
                case ManagedProjectionState.Aborted:
                    _stateHandler = new AbortedState(this);
                    break;
                case ManagedProjectionState.Aborting:
                    _stateHandler = new AbortingState(this);
                    break;
                case ManagedProjectionState.Completed:
                    _stateHandler = new CompletedState(this);
                    break;
                case ManagedProjectionState.Creating:
                case ManagedProjectionState.Loading:
                case ManagedProjectionState.Loaded:
                    _stateHandler = new CreatingLoadingLoadedState(this);
                    break;
                case ManagedProjectionState.Faulted:
                    _stateHandler = new FaultedState(this);
                    break;
                case ManagedProjectionState.LoadingStopped:
                    _stateHandler = new LoadingStateState(this);
                    break;
                case ManagedProjectionState.Prepared:
                    _stateHandler = new PreparedState(this);
                    break;
                case ManagedProjectionState.Preparing:
                    _stateHandler = new PreparingState(this);
                    break;
                case ManagedProjectionState.Running:
                    _stateHandler = new RunningState(this);
                    break;
                case ManagedProjectionState.Starting:
                    _stateHandler = new StartingState(this);
                    break;
                case ManagedProjectionState.Stopped:
                    _stateHandler = new StoppedState(this);
                    break;
                case ManagedProjectionState.Stopping:
                    _stateHandler = new StoppingState(this);
                    break;
                default:
                    throw new Exception();
            }
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

        public void Handle(ProjectionManagementMessage.Command.GetState message)
        {
            _lastAccessed = _timeProvider.Now;
            if (_state >= ManagedProjectionState.Running)
            {
                _getStateDispatcher.Publish(
                    new CoreProjectionManagementMessage.GetState(Guid.NewGuid(), Id, message.Partition, _workerId),
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

        public void Handle(ProjectionManagementMessage.Command.GetResult message)
        {
            _lastAccessed = _timeProvider.Now;
            if (_state >= ManagedProjectionState.Running)
            {
                _getResultDispatcher.Publish(
                    new CoreProjectionManagementMessage.GetResult(Guid.NewGuid(), Id, message.Partition, _workerId),
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

        public void Handle(ProjectionManagementMessage.Command.GetQuery message)
        {
            _lastAccessed = _timeProvider.Now;
            if (!ProjectionManagementMessage.RunAs.ValidateRunAs(Mode, ReadWrite.Read, _runAs, message)) return;

            var emitEnabled = _persistedState.EmitEnabled ?? false;

            var projectionOutputConfig = new ProjectionOutputConfig
            {
                ResultStreamName =
                    _persistedState.SourceDefinition == null
                        ? ""
                        : new ProjectionNamesBuilder(_name, _persistedState.SourceDefinition).GetResultStreamName()
            };

            message.Envelope.ReplyWith(
                new ProjectionManagementMessage.ProjectionQuery(
                    _name,
                    Query,
                    emitEnabled,
                    _persistedState.SourceDefinition,
                    projectionOutputConfig));
        }

        public void Handle(ProjectionManagementMessage.Command.UpdateQuery message)
        {
            _lastAccessed = _timeProvider.Now;
            if (!ProjectionManagementMessage.RunAs.ValidateRunAs(Mode, ReadWrite.Write, _runAs, message)) return;

            _prepared = false;
            DuUpdateQuery1(message);
            UpdateProjectionVersion();
            SetLastReplyEnvelope(message.Envelope);
            StopUnlessPreparedOrLoaded();
        }

        private void SetLastReplyEnvelope(IEnvelope envelope)
        {
            if (_lastReplyEnvelope != null)
                _lastReplyEnvelope.ReplyWith(
                    new ProjectionManagementMessage.OperationFailed("Aborted by subsequent operation"));
            _lastReplyEnvelope = envelope;
        }

        public void Handle(ProjectionManagementMessage.Command.Disable message)
        {
            _lastAccessed = _timeProvider.Now;
            if (!ProjectionManagementMessage.RunAs.ValidateRunAs(Mode, ReadWrite.Write, _runAs, message)) return;
            SetLastReplyEnvelope(message.Envelope);
            if (DoDisable(message.Envelope))
            {
                UpdateProjectionVersion();
                StopUnlessPreparedOrLoaded();
            }
        }

        public void Handle(ProjectionManagementMessage.Command.Abort message)
        {
            _lastAccessed = _timeProvider.Now;
            if (!ProjectionManagementMessage.RunAs.ValidateRunAs(Mode, ReadWrite.Write, _runAs, message)) return;
            UpdateProjectionVersion();
            SetLastReplyEnvelope(message.Envelope);
            if (DoDisable(message.Envelope))
                Abort();
        }

        public void Handle(ProjectionManagementMessage.Command.Enable message)
        {
            _lastAccessed = _timeProvider.Now;
            if (!ProjectionManagementMessage.RunAs.ValidateRunAs(Mode, ReadWrite.Write, _runAs, message)) return;
            if (Enabled
                && !(_state == ManagedProjectionState.Completed || _state == ManagedProjectionState.Faulted
                     || _state == ManagedProjectionState.Aborted || _state == ManagedProjectionState.Loaded
                     || _state == ManagedProjectionState.Prepared || _state == ManagedProjectionState.Stopped))
            {
                message.Envelope.ReplyWith(new ProjectionManagementMessage.OperationFailed("Invalid state"));
                return;
            }
            if (!Enabled)
                Enable();
            _pendingPersistedState = true;
            UpdateProjectionVersion();
            SetLastReplyEnvelope(message.Envelope);
            StopUnlessPreparedOrLoaded();
        }

        public void Handle(ProjectionManagementMessage.Command.SetRunAs message)
        {
            _lastAccessed = _timeProvider.Now;
            if (
                !ProjectionManagementMessage.RunAs.ValidateRunAs(
                    Mode, ReadWrite.Write, _runAs, message,
                    message.Action == ProjectionManagementMessage.Command.SetRunAs.SetRemove.Set)) return;


            _prepared = false;
            DoSetRunAs1(message);
            UpdateProjectionVersion();
            SetLastReplyEnvelope(message.Envelope);
            StopUnlessPreparedOrLoaded();
        }

        public void Handle(ProjectionManagementMessage.Command.Delete message)
        {
            _lastAccessed = _timeProvider.Now;
            if (!ProjectionManagementMessage.RunAs.ValidateRunAs(Mode, ReadWrite.Write, _runAs, message)) return;
            DoDelete();
            UpdateProjectionVersion();
            SetLastReplyEnvelope(message.Envelope);
            StopUnlessPreparedOrLoaded();
        }

        public void Handle(ProjectionManagementMessage.Command.Reset message)
        {
            _lastAccessed = _timeProvider.Now;
            if (!ProjectionManagementMessage.RunAs.ValidateRunAs(Mode, ReadWrite.Write, _runAs, message)) return;
            _prepared = false;
            DoReset1();
            UpdateProjectionVersion();
            SetLastReplyEnvelope(message.Envelope);
            StopUnlessPreparedOrLoaded();
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

                Handle(
                    new ProjectionManagementMessage.Command.Delete(
                        new NoopEnvelope(),
                        _name,
                        ProjectionManagementMessage.RunAs.System,
                        false,
                        false));
            }
        }

        public void Handle(CoreProjectionStatusMessage.Started message)
        {
            _stateHandler.Started();
        }

        public void Handle(CoreProjectionStatusMessage.Stopped message)
        {
            _stateHandler.Stopped(message);
            FireStoppedOrFaulted();
        }

        public void Handle(CoreProjectionStatusMessage.Faulted message)
        {
            _stateHandler.Faulted(message);
            FireStoppedOrFaulted();
        }
         
        public void Handle(CoreProjectionStatusMessage.Prepared message)
        {
            _stateHandler.Prepared(message);
        }

        public void Handle(CoreProjectionStatusMessage.StatisticsReport message)
        {
            _lastReceivedStatistics = message.Statistics;
        }

        private void DoSetRunAs1(ProjectionManagementMessage.Command.SetRunAs message)
        {
            _persistedState.RunAs = message.Action == ProjectionManagementMessage.Command.SetRunAs.SetRemove.Set
                ? SerializedRunAs.SerializePrincipal(message.RunAs)
                : null;
            _runAs = SerializedRunAs.DeserializePrincipal(_persistedState.RunAs);
            _pendingPersistedState = true;
        }

        private void DoReset1()
        {
            _pendingPersistedState = true;
            ResetProjection();
        }

        private void ResetProjection()
        {
            UpdateProjectionVersion(force: true);
            Enabled = false;
            _persistedState.Epoch = _persistedState.Version;
        }

        private void FireStoppedOrFaulted()
        {
            Reply();
        }

        private bool IsExpiredProjection()
        {
            return Mode == ProjectionMode.Transient && !_isSlave && _lastAccessed.AddMinutes(5) < _timeProvider.Now;
        }

        public void InitializeNew(PersistedState persistedState, IEnvelope replyEnvelope)
        {
            LoadPersistedState(persistedState);
            UpdateProjectionVersion();
            _pendingPersistedState = true;
            SetLastReplyEnvelope(replyEnvelope);
            PrepareWriteStartOrLoadStopped();
        }

        public void InitializeExisting(string name)
        {
            SetState(ManagedProjectionState.Loading);
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
                SetState(ManagedProjectionState.Loaded);
                _pendingPersistedState = false;
                SetLastReplyEnvelope(null);
                PrepareWriteStartOrLoadStopped();
                return;
            }

            SetState(ManagedProjectionState.Creating);

            _logger.Trace(
                "Projection manager did not find any projection configuration records in the {0} stream.  Projection stays in CREATING state",
                completed.EventStreamId);
        }

        private void FixUpOldProjectionRunAs(PersistedState persistedState)
        {
            if (persistedState.RunAs == null || string.IsNullOrEmpty(persistedState.RunAs.Name))
            {
                _runAs = SystemAccount.Principal;
                persistedState.RunAs = SerializedRunAs.SerializePrincipal(ProjectionManagementMessage.RunAs.System);
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
            _runAs = SerializedRunAs.DeserializePrincipal(persistedState.RunAs);
        }

        internal void WriteStartOrLoadStopped()
        {
            if (_pendingPersistedState)
                BeginWrite();
            else
                StartOrLoadStopped();
        }

        internal void StartCompleted()
        {
            __OnStarted();
        }

        private void BeginWrite()
        {
            if (Mode == ProjectionMode.Transient)
            {
                //TODO: move to common completion procedure
                _lastWrittenVersion = _persistedState.Version ?? -1;
                StartOrLoadStopped();
                return;
            }
            _writing = true;
            var managedProjectionSerializedState = _persistedState.ToJsonBytes();
            var eventStreamId = "$projections-" + _name;
            var corrId = Guid.NewGuid();
            _writeDispatcher.Publish(
                new ClientMessage.WriteEvents(
                    corrId, corrId, _writeDispatcher.Envelope, true, eventStreamId, ExpectedVersion.Any,
                    new Event(Guid.NewGuid(), "$ProjectionUpdated", true, managedProjectionSerializedState, Empty.ByteArray),
                    SystemAccount.Principal),
                m => WriteCompleted(m, eventStreamId));
        }

        private void WriteCompleted(ClientMessage.WriteEventsCompleted message, string eventStreamId)
        {
            if (!_writing)
            {
                _logger.Error("Projection definition write completed in non writing state. ({0})", _name);
            }
            if (message.Result == OperationResult.Success)
            {
                _logger.Info("'{0}' projection source has been written", _name);
                _pendingPersistedState = false;
                var writtenEventNumber = message.FirstEventNumber;
                if (writtenEventNumber != (_persistedState.Version ?? writtenEventNumber))
                    throw new Exception("Projection version and event number mismatch");
                _lastWrittenVersion = (_persistedState.Version ?? writtenEventNumber);
                StartOrLoadStopped();
                return;
            }
            _logger.Info(
                "Projection '{0}' source has not been written to {1}. Error: {2}",
                _name,
                eventStreamId,
                Enum.GetName(typeof (OperationResult), message.Result));
            if (message.Result == OperationResult.CommitTimeout || message.Result == OperationResult.ForwardTimeout
                || message.Result == OperationResult.PrepareTimeout
                || message.Result == OperationResult.WrongExpectedVersion)
            {
                _logger.Info("Retrying write projection source for {0}", _name);
                BeginWrite();
            }
            else
                throw new NotSupportedException("Unsupported error code received");
        }

        private void Prepare(ProjectionConfig config, Message prepareMessage)
        {
            //_logger.Trace("Request Prepare: {0} {1} ", _name, _state);
            BeginCreate(config, prepareMessage);
        }

        private void Start()
        {
            if (!Enabled)
                throw new InvalidOperationException("Projection is disabled");
            SetState(ManagedProjectionState.Starting);
            _output.Publish(new CoreProjectionManagementMessage.Start(Id, _workerId));
        }

        private void __OnStarted()
        {
            Reply();
        }

        private void LoadStopped()
        {
            SetState(ManagedProjectionState.LoadingStopped);
            _output.Publish(new CoreProjectionManagementMessage.LoadStopped(Id, _workerId));
        }

        private void DisposeCoreProjection()
        {
            _created = false;
            _output.Publish(new CoreProjectionManagementMessage.Dispose(Id, _workerId));
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

        private void BeginCreate(ProjectionConfig config, Message createandPrepapreMessage)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (_state >= ManagedProjectionState.Preparing)
            {
                DisposeCoreProjection();
                SetState(ManagedProjectionState.Loaded);
            }
            //note: set runnign before start as coreProjection.start() can respond with faulted
            SetState(ManagedProjectionState.Preparing);
            _output.Publish(createandPrepapreMessage);
        }

        private Message CreateCreateAndPrepareProjectionMessage(ProjectionConfig config)
        {
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
            return createProjectionMessage;
        }

        private CoreProjectionManagementMessage.CreatePrepared CreateBeginCreatePreparedMessage(ProjectionConfig config)
        {
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
            return createProjectionMessage;
        }

        private void StopUnlessPreparedOrLoaded()
        {
            switch (_state)
            {
                case ManagedProjectionState.Prepared:
                    StoppedOrReadyToStart();
                    break;
                case ManagedProjectionState.Loaded:
                    StoppedOrReadyToStart();
                    break;
                case ManagedProjectionState.Stopped:
                case ManagedProjectionState.Completed:
                case ManagedProjectionState.Aborted:
                case ManagedProjectionState.Faulted:
                    SetState(ManagedProjectionState.Stopped);
                    StoppedOrReadyToStart();
                    return;
                case ManagedProjectionState.Loading:
                case ManagedProjectionState.Creating:
                    throw new InvalidOperationException(
                        string.Format(
                            "Cannot stop a projection in the '{0}' state",
                            Enum.GetName(typeof (ManagedProjectionState), _state)));
                case ManagedProjectionState.Stopping:
                case ManagedProjectionState.Aborting:
                    return;
                case ManagedProjectionState.Running:
                case ManagedProjectionState.Starting:
                    SetState(ManagedProjectionState.Stopping);
                    _output.Publish(new CoreProjectionManagementMessage.Stop(Id, _workerId));
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        private void Abort()
        {
            switch (_state)
            {
                case ManagedProjectionState.Stopped:
                case ManagedProjectionState.Completed:
                case ManagedProjectionState.Aborted:
                case ManagedProjectionState.Faulted:
                case ManagedProjectionState.Loaded:
                    StoppedOrReadyToStart();
                    return;
                case ManagedProjectionState.Loading:
                case ManagedProjectionState.Creating:
                    throw new InvalidOperationException(
                        string.Format(
                            "Cannot stop a projection in the '{0}' state",
                            Enum.GetName(typeof (ManagedProjectionState), _state)));
                case ManagedProjectionState.Stopping:
                    SetState(ManagedProjectionState.Aborting);
                    _output.Publish(new CoreProjectionManagementMessage.Kill(Id, _workerId));
                    return;
                case ManagedProjectionState.Aborting:
                    return;
                case ManagedProjectionState.Running:
                case ManagedProjectionState.Starting:
                    SetState(ManagedProjectionState.Aborting);
                    _output.Publish(new CoreProjectionManagementMessage.Kill(Id, _workerId));
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
            SetState(ManagedProjectionState.Faulted);
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

        private void StartOrLoadStopped()
        {
            if (_state == ManagedProjectionState.Prepared)
            {
                if (Enabled && _enabledToRun)
                    Start();
                else
                {
                    LoadStopped();
                }
            }
            else if (_state == ManagedProjectionState.Aborted || _state == ManagedProjectionState.Completed
                     || _state == ManagedProjectionState.Faulted || _state == ManagedProjectionState.Stopped)
                Reply();
            else
                throw new Exception();
        }


        private void DuUpdateQuery1(ProjectionManagementMessage.Command.UpdateQuery message)
        {
            _persistedState.HandlerType = message.HandlerType ?? HandlerType;
            _persistedState.Query = message.Query;
            _persistedState.EmitEnabled = message.EmitEnabled ?? _persistedState.EmitEnabled;
            _pendingPersistedState = true;
            if (_state == ManagedProjectionState.Completed)
            {
                ResetProjection();
            }
        }

        private bool DoDisable(IEnvelope envelope)
        {
            if (!Enabled)
            {
                envelope.ReplyWith(new ProjectionManagementMessage.OperationFailed("Not enabled"));
                return false;
            }
            Enabled = false;
            _pendingPersistedState = true;
            return true;
        }

        private void PrepareWriteStartOrLoadStopped()
        {
            if (_state == ManagedProjectionState.Prepared)
            {
                WriteStartOrLoadStopped();
                return;
            }

            if (_prepared && _created && !(Enabled && _enabledToRun))
            {
                WriteStartOrLoadStopped();
                return;
            }

            var config = CreateDefaultProjectionConfiguration();

            var prepareMessage = !(Enabled && _enabledToRun) && !_pendingPersistedState
                ? CreateBeginCreatePreparedMessage(config)
                : CreateCreateAndPrepareProjectionMessage(config);

            Prepare(config, prepareMessage);
        }

        private void Reply()
        {
            if (_lastReplyEnvelope != null)
                _lastReplyEnvelope.ReplyWith(new ProjectionManagementMessage.Updated(_name));
            _lastReplyEnvelope = null;
            if (Deleted)
            {
                DisposeCoreProjection();
                _output.Publish(new ProjectionManagementMessage.Internal.Deleted(_name, Id));
            }
        }

        private void DoDelete()
        {
            Enabled = false;
            Deleted = true;
            _pendingPersistedState = true;
        }

        private void UpdateProjectionVersion(bool force = false)
        {
            if (_lastWrittenVersion == _persistedState.Version)
                _persistedState.Version++;
            else if (force)
                throw new ApplicationException("Internal error: projection definition must be saved before forced updating version");
        }

        public void StoppedOrReadyToStart()
        {
            PrepareWriteStartOrLoadStopped();
        }
    }

    public class SerializedRunAs
    {
        public string Name { get; set; }
        public string[] Roles { get; set; }

        public static implicit operator SerializedRunAs(ProjectionManagementMessage.RunAs runAs)
        {
            return runAs == null ? null : SerializePrincipal(runAs);
        }

        public static implicit operator ProjectionManagementMessage.RunAs(SerializedRunAs runAs)
        {
            if (runAs == null)
                return null;
            if (runAs.Name == null)
                return ProjectionManagementMessage.RunAs.Anonymous;
            if (runAs.Name == "$system") //TODO: make sure nobody else uses it
                return ProjectionManagementMessage.RunAs.System;
            return
                new ProjectionManagementMessage.RunAs(
                    new OpenGenericPrincipal(new GenericIdentity(runAs.Name), runAs.Roles));
        }

        public static SerializedRunAs SerializePrincipal(ProjectionManagementMessage.RunAs runAs)
        {
            if (runAs == null)
                return null;
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

        public static IPrincipal DeserializePrincipal(SerializedRunAs runAs)
        {
            if (runAs == null)
                return null;
            if (runAs.Name == null)
                return null;
            if (runAs.Name == "$system") //TODO: make sure nobody else uses it
                return SystemAccount.Principal;
            return new OpenGenericPrincipal(new GenericIdentity(runAs.Name), runAs.Roles);
        }

    }
}
