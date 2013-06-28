// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Collections.Generic;
using System.Security.Principal;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http.Authentication;
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
        private readonly ProjectionStateHandlerFactory _projectionStateHandlerFactory;
        private readonly ITimeProvider _timeProvider;
        private readonly ISingletonTimeoutScheduler _timeoutScheduler;
        private readonly IPublisher _coreQueue;
        private readonly Guid _id;
        private readonly int _projectionId;
        private readonly string _name;
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

        public ManagedProjection(
            IPublisher coreQueue, Guid id, int projectionId, string name, ILogger logger,
            RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> writeDispatcher,
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> readDispatcher,
            IPublisher inputQueue, IPublisher output, ProjectionStateHandlerFactory projectionStateHandlerFactory,
            ITimeProvider timeProvider, ISingletonTimeoutScheduler timeoutScheduler = null)
        {
            if (id == Guid.Empty) throw new ArgumentException("id");
            if (name == null) throw new ArgumentNullException("name");
            if (output == null) throw new ArgumentNullException("output");
            if (name == "") throw new ArgumentException("name");
            _coreQueue = coreQueue;
            _id = id;
            _projectionId = projectionId;
            _name = name;
            _logger = logger;
            _writeDispatcher = writeDispatcher;
            _readDispatcher = readDispatcher;
            _inputQueue = inputQueue;
            _output = output;
            _projectionStateHandlerFactory = projectionStateHandlerFactory;
            _timeProvider = timeProvider;
            _timeoutScheduler = timeoutScheduler;
            _getStateDispatcher =
                new RequestResponseDispatcher
                    <CoreProjectionManagementMessage.GetState, CoreProjectionManagementMessage.StateReport>(
                    coreQueue, v => v.CorrelationId, v => v.CorrelationId, new PublishEnvelope(_inputQueue));
            _getResultDispatcher =
                new RequestResponseDispatcher
                    <CoreProjectionManagementMessage.GetResult, CoreProjectionManagementMessage.ResultReport>(
                    coreQueue, v => v.CorrelationId, v => v.CorrelationId, new PublishEnvelope(_inputQueue));
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
            _coreQueue.Publish(new CoreProjectionManagementMessage.UpdateStatistics(_id));
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
                status.Status = status.Status == "Stopped" && _state == ManagedProjectionState.Completed
                                    ? _state.EnumValueName()
                                    : (!status.Status.StartsWith(_state.EnumValueName())
                                           ? _state.EnumValueName() + "/" + status.Status
                                           : status.Status);
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
                        new PublishEnvelope(_inputQueue), Guid.NewGuid(), _id, message.Partition),
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
            Stop(() => DoDisable(message));
        }

        public void Handle(ProjectionManagementMessage.Enable message)
        {
            _lastAccessed = _timeProvider.Now;
            if (!ProjectionManagementMessage.RunAs.ValidateRunAs(Mode, ReadWrite.Write, _runAs, message)) return;
            if (Enabled && _state == ManagedProjectionState.Running)
            {
                message.Envelope.ReplyWith(
                    new ProjectionManagementMessage.OperationFailed("Already enabled and running"));
                return;
            }
            if (!Enabled)
                Enable();
            Action completed = () =>
                {
                    if (_state == ManagedProjectionState.Prepared)
                        Start(() => message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(message.Name)));
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
                        UpdateProjectionVersion();
                        _persistedState.Epoch = _persistedState.Version;
                        Prepare(
                            () =>
                            BeginWrite(
                                () =>
                                StartOrLoadStopped(
                                    () =>
                                    message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(message.Name)))));
                    });
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
            FireStoppedOrFaulted();
        }

        public void Handle(CoreProjectionManagementMessage.Prepared message)
        {
            _state = ManagedProjectionState.Prepared;
            _persistedState.SourceDefinition = message.SourceDefinition;
            OnPrepared();
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
            if (Mode == ProjectionMode.Transient)
            {
                if (_lastAccessed.AddMinutes(5) < _timeProvider.Now)
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
        }

        public void InitializeNew(ProjectionManagementMessage.Post message, Action completed)
        {
            if (message.Mode >= ProjectionMode.Continuous && !message.CheckpointsEnabled)
                throw new InvalidOperationException("Continuous mode requires checkpoints");

            if (message.EmitEnabled && !message.CheckpointsEnabled)
                throw new InvalidOperationException("Emit requires checkpoints");
            LoadPersistedState(
                new PersistedState
                    {
                        Enabled = message.Enabled,
                        HandlerType = message.HandlerType,
                        Query = message.Query,
                        Mode = message.Mode,
                        EmitEnabled = message.EmitEnabled,
                        CheckpointsDisabled = !message.CheckpointsEnabled,
                        Epoch = -1,
                        Version = -1,
                        RunAs = message.EnableRunAs ? SerializePrincipal(message.RunAs) : null,
                    });
            UpdateProjectionVersion();
            Prepare(() => BeginWrite(() => StartOrLoadStopped(completed)));
        }

        private SerializedRunAs SerializePrincipal(ProjectionManagementMessage.RunAs runAs)
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
                if (Enabled)
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
                completed();
                return;
            }
            var managedProjectionSerializedState = _persistedState.ToJsonBytes();
            var eventStreamId = "$projections-" + _name;
            var corrId = Guid.NewGuid();
            _writeDispatcher.Publish(
                new ClientMessage.WriteEvents(
                    corrId, corrId, _writeDispatcher.Envelope, true, eventStreamId, ExpectedVersion.Any,
                    new Event(Guid.NewGuid(), "$ProjectionUpdated", true, managedProjectionSerializedState, Empty.ByteArray),
                    SystemAccount.Principal),
                m => WriteCompleted(m, completed, eventStreamId));
        }

        private void WriteCompleted(ClientMessage.WriteEventsCompleted message, Action completed, string eventStreamId)
        {
            if (message.Result == OperationResult.Success)
            {
                _logger.Info("'{0}' projection source has been written", _name);
                var writtenEventNumber = message.FirstEventNumber;
                if (writtenEventNumber != (_persistedState.Version ?? writtenEventNumber))
                    throw new Exception("Projection version and event number mismatch");
                _lastWrittenVersion = (_persistedState.Version ?? writtenEventNumber);
                if (completed != null) completed();
                return;
            }
            _logger.Info("Projection '{0}' source has not been written to {1}. Error: {2}",
                         _name,
                         eventStreamId,
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
            BeginCreateAndPrepare(_projectionStateHandlerFactory, config, onPrepared);
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
            _coreQueue.Publish(new CoreProjectionManagementMessage.Start(_id));
        }

        private void LoadStopped(Action onLoaded)
        {
            _onStopped = onLoaded;
            _state = ManagedProjectionState.LoadingState;
            _coreQueue.Publish(new CoreProjectionManagementMessage.LoadStopped(_id));
        }

        private void DisposeCoreProjection()
        {
            _coreQueue.Publish(new CoreProjectionManagementMessage.Dispose(_id));
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

        private void UpdateQuery(string handlerType, string query, bool? emitEnabled)
        {
            _persistedState.HandlerType = handlerType;
            _persistedState.Query = query;
            _persistedState.EmitEnabled = emitEnabled ?? _persistedState.EmitEnabled;
        }

        private void BeginCreateAndPrepare(
            ProjectionStateHandlerFactory handlerFactory, ProjectionConfig config, Action onPrepared)
        {
            _onPrepared = _onStopped = () =>
                {
                    _onStopped = null;
                    _onPrepared = null;
                    if (onPrepared != null) 
                        onPrepared();
                };
            if (handlerFactory == null) throw new ArgumentNullException("handlerFactory");
            if (config == null) throw new ArgumentNullException("config");

            //TODO: which states are allowed here?
            if (_state >= ManagedProjectionState.Preparing)
            {
                DisposeCoreProjection();
                _state = ManagedProjectionState.Loaded;
            }

            //TODO: load configuration from the definition


            var createProjectionMessage =
                new CoreProjectionManagementMessage.CreateAndPrepare(
                    new PublishEnvelope(_inputQueue), _id, _name, 
                    new ProjectionVersion(_projectionId, _persistedState.Epoch ?? 0, _persistedState.Version ?? 0),
                    config, delegate
                        {
                            // this delegate runs in the context of a projection core thread
                            // TODO: move this code to the projection core service as we may be in different processes in the future
                            IProjectionStateHandler stateHandler = null;
                            try
                            {
                                stateHandler = handlerFactory.Create(
                                    HandlerType, Query, logger: Console.WriteLine,
                                    cancelCallbackFactory:
                                        _timeoutScheduler == null
                                            ? (Action<int, Action>) null
                                            : _timeoutScheduler.Schedule);
                                return stateHandler;
                            }
                            catch (Exception ex)
                            {
                                SetFaulted(
                                    string.Format(
                                        "Cannot create a projection state handler.\r\n\r\nHandler type: {0}\r\nQuery:\r\n\r\n{1}\r\n\r\nMessage:\r\n\r\n{2}",
                                        HandlerType, Query, ex.Message), ex);
                                if (stateHandler != null)
                                    stateHandler.Dispose();
                                throw;
                            }
                        });

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

            var createProjectionMessage =
                new CoreProjectionManagementMessage.CreatePrepared(
                    new PublishEnvelope(_inputQueue), _id, _name, new ProjectionVersion(_projectionId, _persistedState.Epoch ?? 0,
                    _persistedState.Version ?? 1), config, new SourceDefinition(_persistedState.SourceDefinition));

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
                    _coreQueue.Publish(new CoreProjectionManagementMessage.Stop(_id));
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

            var projectionConfig = new ProjectionConfig(_runAs,
                checkpointHandledThreshold, checkpointUnhandledBytesThreshold, pendingEventsThreshold,
                maxWriteBatchLength, emitEventEnabled, checkpointsEnabled, createTempStreams, stopOnEof);
            return projectionConfig;
        }

        private void StartOrLoadStopped(Action completed)
        {
            if (_state == ManagedProjectionState.Prepared)
            {
                if (Enabled)
                    Start(completed);
                else
                    LoadStopped(completed);
            }
            else if (completed != null)
                completed();
        }


        private void DoUpdateQuery(ProjectionManagementMessage.UpdateQuery message)
        {
            UpdateQuery(message.HandlerType ?? HandlerType, message.Query, message.EmitEnabled);
            Action completed = () =>
                {
                    StartOrLoadStopped(() => { });
                    message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(message.Name));
                };
            UpdateProjectionVersion();
            Prepare(() => BeginWrite(completed));
        }

        private void DoDisable(ProjectionManagementMessage.Disable message)
        {
            if (!Enabled)
            {
                message.Envelope.ReplyWith(new ProjectionManagementMessage.OperationFailed("Not enabled"));
                return;
            }
            Disable();
            Action completed = () => message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(message.Name));
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
                    _output.Publish(new ProjectionManagementMessage.Internal.Deleted(_name, _id));
                };
            UpdateProjectionVersion();
            if (Enabled)
                Prepare(() => BeginWrite(completed));
            else
                BeginWrite(completed);
        }

        private void UpdateProjectionVersion()
        {
            if (_lastWrittenVersion == _persistedState.Version)
                _persistedState.Version++;
        }

        public void Handle(ProjectionManagementMessage.GetState message)
        {
            _lastAccessed = _timeProvider.Now;
            if (_state >= ManagedProjectionState.Stopped)
            {
                _getStateDispatcher.Publish(
                    new CoreProjectionManagementMessage.GetState(
                        new PublishEnvelope(_inputQueue), Guid.NewGuid(), _id, message.Partition),
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
    }

    public class SerializedRunAs
    {
        public string Name { get; set; }
        public string[] Roles { get; set; }
    }

    class SourceDefinition : ISourceDefinitionConfigurator
    {
        private readonly ProjectionSourceDefinition _sourceDefinition;

        public SourceDefinition(ProjectionSourceDefinition sourceDefinition)
        {
            if (sourceDefinition == null) throw new ArgumentNullException("sourceDefinition");

            _sourceDefinition = sourceDefinition;
        }

        public void ConfigureSourceProcessingStrategy(QuerySourceProcessingStrategyBuilder builder)
        {
            if (_sourceDefinition.AllEvents) builder.AllEvents();

            if (_sourceDefinition.AllStreams) builder.FromAll();

            if (_sourceDefinition.Categories != null)
                foreach (var category in _sourceDefinition.Categories)
                    builder.FromCategory(category);
            if (_sourceDefinition.Streams != null)
                foreach (var stream in _sourceDefinition.Streams)
                    builder.FromStream(stream);

            if (_sourceDefinition.Events != null)
                foreach (var @event in _sourceDefinition.Events)
                    builder.IncludeEvent(@event);

            if (_sourceDefinition.ByStream)
                builder.SetByStream();

            //TODO: set false if options == null?

            if (_sourceDefinition.Options != null && _sourceDefinition.Options.IncludeLinks)
                builder.SetIncludeLinks(_sourceDefinition.Options.IncludeLinks);

            if (_sourceDefinition.Options != null && !string.IsNullOrEmpty(_sourceDefinition.Options.ResultStreamName))
                builder.SetResultStreamNameOption(_sourceDefinition.Options.ResultStreamName);

            if (_sourceDefinition.Options != null && !string.IsNullOrEmpty(_sourceDefinition.Options.PartitionResultStreamNamePattern))
                builder.SetPartitionResultStreamNamePatternOption(_sourceDefinition.Options.PartitionResultStreamNamePattern);

            if (_sourceDefinition.Options != null && !string.IsNullOrEmpty(_sourceDefinition.Options.ForceProjectionName))
                builder.SetForceProjectionName(_sourceDefinition.Options.ForceProjectionName);

            if (_sourceDefinition.Options != null)
                builder.SetReorderEvents(_sourceDefinition.Options.ReorderEvents);

            if (_sourceDefinition.Options != null)
                builder.SetProcessingLag(_sourceDefinition.Options.ProcessingLag);

            if (_sourceDefinition.DefinesStateTransform)
                builder.SetDefinesStateTransform();
        }
    }
}
