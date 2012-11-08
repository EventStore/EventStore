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
        }

        private readonly IPublisher _inputQueue;

        private readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>
            _writeDispatcher;

        private readonly
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>
            _readDispatcher;


        private readonly ILogger _logger;
        private readonly ProjectionStateHandlerFactory _projectionStateHandlerFactory;
        private readonly IPublisher _coreQueue;
        private readonly Guid _id;
        private readonly string _name;
        private ManagedProjectionState _state;
        private PersistedState _persistedState = new PersistedState();

        private string _faultedReason;
        private Action _stopCompleted;
        private Dictionary<string, List<IEnvelope>> _stateRequests;
        private ProjectionStatistics _lastReceivedStatistics;
        private Action<CoreProjectionManagementMessage.Prepared> _onPrepared;

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
        }

        private string HandlerType
        {
            get { return _persistedState.HandlerType; }
            set { _persistedState.HandlerType = value; }
        }

        private string Query
        {
            get { return _persistedState.Query; }
            set { _persistedState.Query = value; }
        }

        private ProjectionMode Mode
        {
            get { return _persistedState.Mode; }
            set { _persistedState.Mode = value; }
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
            if (_lastReceivedStatistics == null || _state != ManagedProjectionState.Running)
            {
                status = new ProjectionStatistics
                    {Name = _name, Mode = GetMode(), Status = _state.EnumVaueName(), MasterStatus = _state};
            }
            else
            {
                status = _lastReceivedStatistics.Clone();
                status.Name = _name;
                status.Status = _state.EnumVaueName() + "/" + status.Status;
                status.MasterStatus = _state;
            }
            if (_state == ManagedProjectionState.Faulted)
                status.StateReason = _faultedReason;
            return status;
        }

        public void Handle(ProjectionManagementMessage.GetQuery message)
        {
            message.Envelope.ReplyWith(new ProjectionManagementMessage.ProjectionQuery(_name, Query));
        }

        public void Handle(ProjectionManagementMessage.UpdateQuery message)
        {
            Stop(() => DoUpdateQuery(message));
        }

        public void Handle(ProjectionManagementMessage.GetState message)
        {
            if (_state == ManagedProjectionState.Running)
            {
                var needRequest = false;
                if (_stateRequests == null)
                {
                    _stateRequests = new Dictionary<string, List<IEnvelope>>();
                }
                List<IEnvelope> partitionRequests;
                if (!_stateRequests.TryGetValue(message.Partition, out partitionRequests))
                {
                    partitionRequests = new List<IEnvelope>();
                    _stateRequests.Add(message.Partition, partitionRequests);
                    needRequest = true;
                }
                partitionRequests.Add(message.Envelope);
                if (needRequest)
                    _coreQueue.Publish(
                        new CoreProjectionManagementMessage.GetState(
                            new PublishEnvelope(_inputQueue), _id, message.Partition));
            }
            else
            {
                //TODO: report right state here
                message.Envelope.ReplyWith(
                    new ProjectionManagementMessage.ProjectionState(message.Name, "*** UNKNOWN ***"));
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
                message.Envelope.ReplyWith(new ProjectionManagementMessage.OperationFailed("Not disabled"));
                return;
            }
            Enable();
            PrepareAndBeginWrite(
                forcePrepare: true, completed: () =>
                    {
                        message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(message.Name));
                        StartIfEnabled();
                    });
        }

        public void Handle(CoreProjectionManagementMessage.Started message)
        {
            _state = ManagedProjectionState.Running;
        }

        public void Handle(CoreProjectionManagementMessage.Stopped message)
        {
            _state = ManagedProjectionState.Stopped;
            DisposeCoreProjection();
            var stopCompleted = _stopCompleted;
            _stopCompleted = null;
            if (stopCompleted != null) stopCompleted();
        }

        public void Handle(CoreProjectionManagementMessage.Faulted message)
        {
            SetFaulted(message.FaultedReason);
            DisposeCoreProjection();
        }

        public void Handle(CoreProjectionManagementMessage.Prepared message)
        {
            _state = ManagedProjectionState.Prepared;
            if (_onPrepared != null)
            {
                var action = _onPrepared;
                _onPrepared = null;
                action(message);
            }
        }

        public void Handle(CoreProjectionManagementMessage.StateReport message)
        {
            var partitionRequests = _stateRequests[message.Partition];
            _stateRequests.Remove(message.Partition);

            foreach (var request in partitionRequests)
                request.ReplyWith(new ProjectionManagementMessage.ProjectionState(_name, message.State));
        }

        public void Handle(CoreProjectionManagementMessage.StatisticsReport message)
        {
            _lastReceivedStatistics = message.Statistics;
        }

        public void InitializeNew(ProjectionManagementMessage.Post message, Action completed)
        {
            LoadPersistedState(
                new PersistedState
                    {
                        Enabled = message.Enabled,
                        HandlerType = message.HandlerType,
                        Query = message.Query,
                        Mode = message.Mode
                    });
            PrepareAndBeginWrite(forcePrepare: true, completed: () => StartNew(completed));
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
                    Guid.NewGuid(), _readDispatcher.Envelope, "$projections-" + name, -1, 1, resolveLinks: false),
                LoadCompleted);
        }

        private void LoadCompleted(ClientMessage.ReadStreamEventsBackwardCompleted completed)
        {
            if (completed.Result == RangeReadResult.Success && completed.Events.Length == 1)
            {
                byte[] state = completed.Events[0].Event.Data;
                LoadPersistedState(state.ParseJson<PersistedState>());
                //TODO: encapsulate this into managed projection
                _state = ManagedProjectionState.Stopped;
                StartIfEnabled();
                return;
            }

            _state = ManagedProjectionState.Creating;

            _logger.Trace(
                "Projection manager did not find any projection configuration records in the {0} stream.  Projection stays in CREATING state",
                completed.EventStreamId);
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

        private void PrepareAndBeginWrite(bool forcePrepare, Action completed)
        {
            BeginWrite(completed);
        }

        private void BeginWrite(Action completed)
        {
            if (Mode <= ProjectionMode.AdHoc)
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

        private void StartIfEnabled()
        {
            if (Enabled)
            {
                var config = CreateDefaultProjectionConfiguration(GetMode());
                PrepareAndStart(_projectionStateHandlerFactory, config);
            }
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

        private void UpdateQuery(string handlerType, string query)
        {
            HandlerType = handlerType;
            Query = query;
        }

        private void PrepareAndStart(ProjectionStateHandlerFactory handlerFactory, ProjectionConfig config)
        {
            if (handlerFactory == null) throw new ArgumentNullException("handlerFactory");
            if (config == null) throw new ArgumentNullException("config");

            //TODO: which states are allowed here?
            if (_state >= ManagedProjectionState.Preparing)
                throw new InvalidOperationException("Already preparing or has been prepared");

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
                                checkpointStrategyBuilder.Validate(Mode); // avoid future exceptions in coreprojection
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
            _onPrepared = delegate
                {
                    _state = ManagedProjectionState.Starting;
                    _coreQueue.Publish(new CoreProjectionManagementMessage.Start(_id));
                };
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
                    _stopCompleted += completed;
                    return;
                case ManagedProjectionState.Running:
                case ManagedProjectionState.Starting:
                    _state = ManagedProjectionState.Stopping;
                    _stopCompleted = completed;
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

        private static ProjectionConfig CreateDefaultProjectionConfiguration(ProjectionMode mode)
        {
            var projectionConfig = new ProjectionConfig(
                mode, mode > ProjectionMode.AdHoc ? 2000 : 0, mode > ProjectionMode.AdHoc ? 10*1000*1000 : 0, 1000, 500,
                publishStateUpdates: mode == ProjectionMode.Persistent, checkpointsEnabled: mode > ProjectionMode.AdHoc,
                emitEventEnabled: mode == ProjectionMode.Persistent); //TODO: allow emit in continuous
            return projectionConfig;
        }

        private void StartNew(Action completed)
        {
            _state = ManagedProjectionState.Stopped;
            StartIfEnabled();
            if (completed != null) completed();
        }

        private void DoUpdateQuery(ProjectionManagementMessage.UpdateQuery message)
        {
            UpdateQuery(message.HandlerType ?? HandlerType, message.Query);
            PrepareAndBeginWrite(
                forcePrepare: true, completed: () =>
                    {
                        StartIfEnabled();
                        message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(message.Name));
                    });
        }

        private void DoDisable(ProjectionManagementMessage.Disable message)
        {
            if (!Enabled)
            {
                message.Envelope.ReplyWith(new ProjectionManagementMessage.OperationFailed("Not enabled"));
                return;
            }
            Disable();
            PrepareAndBeginWrite(
                forcePrepare: false,
                completed: () => message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(message.Name)));
        }

        public void Handle(ProjectionManagementMessage.Delete message)
        {
            Stop(() => DoDelete(message));
        }

        private void DoDelete(ProjectionManagementMessage.Delete message)
        {
            if (Enabled)
                Disable();
            Delete();
            PrepareAndBeginWrite(
                forcePrepare: false,
                completed: () => message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(message.Name)));
        }
    }
}
