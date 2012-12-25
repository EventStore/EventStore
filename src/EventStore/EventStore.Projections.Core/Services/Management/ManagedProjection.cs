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
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Utils;
using System.Linq;

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
            public ProjectionSourceDefintion SourceDefintion { get; set; }
            public bool? EmitEnabled { get; set; }
            public bool? CreateTempStreams { get; set; }
            public bool? CheckpointsDisabled { get; set; }
        }

        private readonly IPublisher _inputQueue;

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


        private readonly ILogger _logger;
        private readonly ProjectionStateHandlerFactory _projectionStateHandlerFactory;
        private readonly IPublisher _coreQueue;
        private readonly Guid _id;
        private readonly string _name;
        private ManagedProjectionState _state;
        private PersistedState _persistedState = new PersistedState();

        private string _faultedReason;
        private Action _onStopped;
        private List<IEnvelope> _debugStateRequests;
        private ProjectionStatistics _lastReceivedStatistics;
        private Action _onPrepared;
        private Action _onStarted;

        public ManagedProjection(
            IPublisher coreQueue, Guid id, string name, ILogger logger,
            RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> writeDispatcher,
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> readDispatcher,
            IPublisher inputQueue, ProjectionStateHandlerFactory projectionStateHandlerFactory)
        {
            if (id == Guid.Empty) throw new ArgumentException("id");
            if (name == null) throw new ArgumentNullException("name");
            if (name == "") throw new ArgumentException("name");
            _coreQueue = coreQueue;
            _id = id;
            _name = name;
            _logger = logger;
            _writeDispatcher = writeDispatcher;
            _readDispatcher = readDispatcher;
            _inputQueue = inputQueue;
            _projectionStateHandlerFactory = projectionStateHandlerFactory;
            _getStateDispatcher =
                new RequestResponseDispatcher
                    <CoreProjectionManagementMessage.GetState, CoreProjectionManagementMessage.StateReport>(
                    coreQueue, v => v.CorrelationId, v => v.CorrelationId, new PublishEnvelope(_inputQueue));
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
                    {Name = _name, Mode = GetMode(), Status = _state.EnumVaueName(), MasterStatus = _state};
            }
            else
            {
                status = _lastReceivedStatistics.Clone();
                status.Mode = GetMode();
                status.Name = _name;
                status.Status = !status.Status.StartsWith(_state.EnumVaueName())
                                    ? _state.EnumVaueName() + "/" + status.Status
                                    : status.Status;
                status.MasterStatus = _state;
            }
            if (_state == ManagedProjectionState.Faulted)
                status.StateReason = _faultedReason;
            return status;
        }

        public void Handle(ProjectionManagementMessage.GetQuery message)
        {
            var emitEnabled = _persistedState.EmitEnabled ?? false;
            message.Envelope.ReplyWith(new ProjectionManagementMessage.ProjectionQuery(_name, Query, emitEnabled));
        }

        public void Handle(ProjectionManagementMessage.UpdateQuery message)
        {
            Stop(() => DoUpdateQuery(message));
        }

        public void Handle(ProjectionManagementMessage.GetState message)
        {
            if (_state >= ManagedProjectionState.Stopped)
            {
                _getStateDispatcher.Publish(
                    new CoreProjectionManagementMessage.GetState(
                        new PublishEnvelope(_inputQueue), Guid.NewGuid(), _id, message.Partition),
                    m =>
                    message.Envelope.ReplyWith(
                        new ProjectionManagementMessage.ProjectionState(_name, m.Partition, m.State)));
            }
            else
            {
                message.Envelope.ReplyWith(
                    new ProjectionManagementMessage.ProjectionState(message.Name, message.Partition, "*** UNKNOWN ***"));
            }
        }

        public void Handle(ProjectionManagementMessage.GetDebugState message)
        {
            if (_state >= ManagedProjectionState.Stopped)
            {
                var needRequest = false;
                if (_debugStateRequests == null)
                {
                    _debugStateRequests = new List<IEnvelope>();
                    needRequest = true;
                }
                _debugStateRequests.Add(message.Envelope);
                if (needRequest)
                    _coreQueue.Publish(
                        new CoreProjectionManagementMessage.GetDebugState(new PublishEnvelope(_inputQueue), _id));
            }
            else
            {
                //TODO: report right state here
                message.Envelope.ReplyWith(
                    new ProjectionManagementMessage.ProjectionDebugState(message.Name, null));
            }
        }

        public void Handle(ProjectionManagementMessage.Disable message)
        {
            Stop(() => DoDisable(message));
        }

        public void Handle(ProjectionManagementMessage.Enable message)
        {
            if (Enabled)
            {
                _logger.Error("DBG: *{0}* ALREADY ENABLED!!!", _name);
                message.Envelope.ReplyWith(new ProjectionManagementMessage.OperationFailed("Not disabled"));
                return;
            }
            Enable();
            Action completed = () => Start(() => message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(message.Name)));
            Prepare(() => BeginWrite(completed));
        }

        public void Handle(ProjectionManagementMessage.Delete message)
        {
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
            _state = ManagedProjectionState.Stopped;
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
                _persistedState.SourceDefintion = null;
                OnPrepared();
            }
            FireStoppedOrFaulted();
        }

        public void Handle(CoreProjectionManagementMessage.Prepared message)
        {
            _state = ManagedProjectionState.Prepared;
            _persistedState.SourceDefintion = message.SourceDefintion;
            OnPrepared();
        }

        public void Handle(CoreProjectionManagementMessage.StateReport message)
        {
            _getStateDispatcher.Handle(message);
        }

        public void Handle(CoreProjectionManagementMessage.StatisticsReport message)
        {
            _lastReceivedStatistics = message.Statistics;
        }

        public void Handle(CoreProjectionManagementMessage.DebugState message)
        {
            var debugStateRequests = _debugStateRequests;
            _debugStateRequests = null;
            foreach (var request in debugStateRequests)
                request.ReplyWith(new ProjectionManagementMessage.ProjectionDebugState(_name, message.Events));
        }

        public void InitializeNew(ProjectionManagementMessage.Post message, Action completed)
        {
            LoadPersistedState(
                new PersistedState
                    {
                        Enabled = message.Enabled,
                        HandlerType = message.HandlerType,
                        Query = message.Query,
                        Mode = message.Mode,
                        EmitEnabled = message.EmitEnabled,
                    });
            Action completed1 = () => StartOrLoadNew(completed);
            Prepare(() => BeginWrite(completed1));
        }

        public void InitializeExisting(string name)
        {
            _state = ManagedProjectionState.Loading;
            BeginLoad(name);
        }

        private void BeginLoad(string name)
        {
            _readDispatcher.Publish(
                new ClientMessage.ReadStreamEventsBackward(
                    Guid.NewGuid(), _readDispatcher.Envelope, "$projections-" + name, -1, 1, resolveLinks: false, validationStreamVersion: null),
                LoadCompleted);
        }

        private void LoadCompleted(ClientMessage.ReadStreamEventsBackwardCompleted completed)
        {
            if (completed.Result == StreamResult.Success && completed.Events.Length == 1)
            {
                byte[] state = completed.Events[0].Event.Data;
                var persistedState = state.ParseJson<PersistedState>();
                FixupOldProjectionModes(persistedState);
                LoadPersistedState(persistedState);
                //TODO: encapsulate this into managed projection
                _state = ManagedProjectionState.Loaded;
                if (Enabled)
                    Prepare(() => Start(() => { }));
                else
                    CreatePrepared(() => LoadStopped(() => { }));
                return;
            }

            _state = ManagedProjectionState.Creating;

            _logger.Trace(
                "Projection manager did not find any projection configuration records in the {0} stream.  Projection stays in CREATING state",
                completed.EventStreamId);
        }

        private void FixupOldProjectionModes(PersistedState persistedState)
        {
            switch ((int)persistedState.Mode)
            {
                case 1: //old AdHoc
                    persistedState.Mode = ProjectionMode.OneTime;
                    break;
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
            if (Mode == ProjectionMode.OneTime)
            {
                completed();
                return;
            }
            var managedProjectionSerializedState = _persistedState.ToJsonBytes();
            _writeDispatcher.Publish(
                new ClientMessage.WriteEvents(
                    Guid.NewGuid(), _writeDispatcher.Envelope, true, "$projections-" + _name, ExpectedVersion.Any,
                    new Event(Guid.NewGuid(), "ProjectionUpdated", false, managedProjectionSerializedState, new byte[0])),
                m => WriteCompleted(m, completed));
        }

        private void WriteCompleted(ClientMessage.WriteEventsCompleted message, Action completed)
        {
            if (message.ErrorCode == OperationErrorCode.Success)
            {
                _logger.Info("'{0}' projection source has been written", _name);
                if (completed != null) completed();
                return;
            }
            _logger.Info(
                "Projection '{0}' source has not been written to {1}. Error: {2}", _name, message.EventStreamId,
                Enum.GetName(typeof (OperationErrorCode), message.ErrorCode));
            if (message.ErrorCode == OperationErrorCode.CommitTimeout
                || message.ErrorCode == OperationErrorCode.ForwardTimeout
                || message.ErrorCode == OperationErrorCode.PrepareTimeout
                || message.ErrorCode == OperationErrorCode.WrongExpectedVersion)
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

        private void Start(Action onStarted)
        {
            if (!Enabled)
                throw new InvalidOperationException("Projection is disabled");
            _onStarted = onStarted;
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
            _onPrepared = onPrepared;
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
                    new PublishEnvelope(_inputQueue), _id, _name, config, delegate
                        {
                            // this delegate runs in the context of a projection core thread
                            // TODO: move this code to the projection core service as we may be in different processes in the future
                            IProjectionStateHandler stateHandler = null;
                            try
                            {
                                stateHandler = handlerFactory.Create(HandlerType, Query, Console.WriteLine);
                                var checkpointStrategyBuilder = new CheckpointStrategy.Builder();
                                stateHandler.ConfigureSourceProcessingStrategy(checkpointStrategyBuilder);
                                checkpointStrategyBuilder.Validate(config); // avoid future exceptions in coreprojection
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

            if (_persistedState.SourceDefintion == null)
                throw new Exception("The projection cannot be loaded as stopped as it was stored in the old format.  Update the projection query text to force prepare");

            var createProjectionMessage =
                new CoreProjectionManagementMessage.CreatePrepared(
                    new PublishEnvelope(_inputQueue), _id, _name, config,
                    new SourceDefintion(_persistedState.SourceDefintion));

            //note: set runnign before start as coreProjection.start() can respond with faulted
            _state = ManagedProjectionState.Preparing;
            _coreQueue.Publish(createProjectionMessage);
        }

        private void Stop(Action completed)
        {
            switch (_state)
            {
                case ManagedProjectionState.Stopped:
                case ManagedProjectionState.Faulted:
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
            var checkpointHandledThreshold = checkpointsEnabled ? 2000 : 0;
            var checkpointUnhandledBytesThreshold = checkpointsEnabled ? 10*1000*1000 : 0;
            var pendingEventsThreshold = 1000;
            var maxWriteBatchLength = 500;
            var emitEventEnabled = _persistedState.EmitEnabled == true;
            var createTempStreams = _persistedState.CreateTempStreams == true;
            var stopOnEof = _persistedState.Mode == ProjectionMode.OneTime;

            var projectionConfig = new ProjectionConfig(
                checkpointHandledThreshold, checkpointUnhandledBytesThreshold, pendingEventsThreshold,
                maxWriteBatchLength, emitEventEnabled, checkpointsEnabled, createTempStreams, stopOnEof); 
            return projectionConfig;
        }

        private void StartOrLoadNew(Action completed)
        {
            if (Enabled)
                Start(completed);
            else
                LoadStopped(completed);
        }

        private void DoUpdateQuery(ProjectionManagementMessage.UpdateQuery message)
        {
            UpdateQuery(message.HandlerType ?? HandlerType, message.Query, message.EmitEnabled);
            Action completed = () =>
                {
                    if (Enabled)
                        Start(() => { });
                    else
                        LoadStopped(() => { });
                    message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(message.Name));
                };
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
                    message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(message.Name));
                    DisposeCoreProjection();
                };
            if (Enabled)
                Prepare(() => BeginWrite(completed));
            else
                BeginWrite(completed);
        }
    }

    class SourceDefintion : ISourceDefinitionConfigurator
    {
        private readonly ProjectionSourceDefintion _sourceDefintion;

        public SourceDefintion(ProjectionSourceDefintion sourceDefintion)
        {
            if (sourceDefintion == null) throw new ArgumentNullException("sourceDefintion");

            _sourceDefintion = sourceDefintion;
        }

        public void ConfigureSourceProcessingStrategy(QuerySourceProcessingStrategyBuilder builder)
        {
            if (_sourceDefintion.AllEvents) builder.AllEvents();

            if (_sourceDefintion.AllStreams) builder.FromAll();

            if (_sourceDefintion.Categories != null)
                foreach (var category in _sourceDefintion.Categories)
                    builder.FromCategory(category);
            if (_sourceDefintion.Streams != null)
                foreach (var stream in _sourceDefintion.Streams)
                    builder.FromStream(stream);

            if (_sourceDefintion.Events != null)
                foreach (var @event in _sourceDefintion.Events)
                    builder.IncludeEvent(@event);

            if (_sourceDefintion.ByStream)
                builder.SetByStream();

            //TODO: set false if options == null?

            if (_sourceDefintion.Options != null && !string.IsNullOrEmpty(_sourceDefintion.Options.StateStreamName))
                builder.SetStateStreamNameOption(_sourceDefintion.Options.StateStreamName);

            if (_sourceDefintion.Options != null && !string.IsNullOrEmpty(_sourceDefintion.Options.ForceProjectionName))
                builder.SetForceProjectionName(_sourceDefintion.Options.ForceProjectionName);

            if (_sourceDefintion.Options != null)
                builder.SetUseEventIndexes(_sourceDefintion.Options.UseEventIndexes);

            if (_sourceDefintion.Options != null)
                builder.SetReorderEvents(_sourceDefintion.Options.ReorderEvents);

            if (_sourceDefintion.Options != null)
                builder.SetProcessingLag(_sourceDefintion.Options.ProcessingLag);

            if (_sourceDefintion.Options != null)
                builder.SetEmitStateUpdated(_sourceDefintion.Options.EmitStateUpdated);
        }
    }
}
