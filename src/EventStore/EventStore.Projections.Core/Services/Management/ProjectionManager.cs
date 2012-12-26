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
using System.Linq;
using System.Text;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Standard;

namespace EventStore.Projections.Core.Services.Management
{
    public class ProjectionManager : IDisposable,
                                     IHandle<SystemMessage.StateChangeMessage>,
                                     IHandle<ClientMessage.ReadStreamEventsBackwardCompleted>,
                                     IHandle<ClientMessage.WriteEventsCompleted>,
                                     IHandle<ProjectionManagementMessage.Post>,
                                     IHandle<ProjectionManagementMessage.UpdateQuery>,
                                     IHandle<ProjectionManagementMessage.GetQuery>,
                                     IHandle<ProjectionManagementMessage.Delete>,
                                     IHandle<ProjectionManagementMessage.GetStatistics>,
                                     IHandle<ProjectionManagementMessage.GetState>,
                                     IHandle<ProjectionManagementMessage.GetDebugState>,
                                     IHandle<ProjectionManagementMessage.Disable>,
                                     IHandle<ProjectionManagementMessage.Enable>,
                                     IHandle<CoreProjectionManagementMessage.Started>,
                                     IHandle<CoreProjectionManagementMessage.Stopped>,
                                     IHandle<CoreProjectionManagementMessage.Faulted>,
                                     IHandle<CoreProjectionManagementMessage.Prepared>,
                                     IHandle<CoreProjectionManagementMessage.StateReport>,
                                     IHandle<CoreProjectionManagementMessage.DebugState>,
                                     IHandle<CoreProjectionManagementMessage.StatisticsReport>
    {
        private readonly ILogger _logger = LogManager.GetLoggerFor<ProjectionManager>();

        private readonly IPublisher _inputQueue;
        private readonly IPublisher _publisher;
        private readonly IPublisher[] _queues;
        private readonly ProjectionStateHandlerFactory _projectionStateHandlerFactory;
        private readonly Dictionary<string, ManagedProjection> _projections;
        private readonly Dictionary<Guid, string> _projectionsMap;

        private readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>
            _writeDispatcher;

        private readonly
            RequestResponseDispatcher<ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>
            _readDispatcher;

        private int _readEventsBatchSize = 100;

        public ProjectionManager(IPublisher inputQueue, IPublisher publisher, IPublisher[] queues)
        {
            if (inputQueue == null) throw new ArgumentNullException("inputQueue");
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (queues == null) throw new ArgumentNullException("queues");
            if (queues.Length == 0) throw new ArgumentException("queues");

            _inputQueue = inputQueue;
            _publisher = publisher;
            _queues = queues;

            _writeDispatcher =
                new RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>(
                    publisher, v => v.CorrelationId, v => v.CorrelationId, new PublishEnvelope(_inputQueue));
            _readDispatcher =
                new RequestResponseDispatcher
                    <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>(
                    publisher, v => v.CorrelationId, v => v.CorrelationId, new PublishEnvelope(_inputQueue));


            _projectionStateHandlerFactory = new ProjectionStateHandlerFactory();
            _projections = new Dictionary<string, ManagedProjection>();
            _projectionsMap = new Dictionary<Guid, string>();
        }

        private void Start()
        {
            _started = true;
            foreach (var queue in _queues)
                queue.Publish(new ProjectionCoreServiceMessage.Start());
            StartExistingProjections();
        }

        private void Stop()
        {
            _started = false;
            foreach (var queue in _queues)
                queue.Publish(new ProjectionCoreServiceMessage.Stop());

            _writeDispatcher.CancelAll();
            _readDispatcher.CancelAll();

            _projections.Clear();
            _projectionsMap.Clear();
        }

        public void Handle(ProjectionManagementMessage.Post message)
        {
            if (message.Name == null)
            {
                message.Envelope.ReplyWith(
                    new ProjectionManagementMessage.OperationFailed("Projection name is required"));
            }
            else
            {
                if (_projections.ContainsKey(message.Name))
                {
                    message.Envelope.ReplyWith(
                        new ProjectionManagementMessage.OperationFailed("Duplicate projection name: " + message.Name));
                }
                else
                {
                    PostNewProjection(
                        message,
                        managedProjection =>
                        message.Envelope.ReplyWith(new ProjectionManagementMessage.Updated(message.Name)));
                }
            }
        }

        public void Handle(ProjectionManagementMessage.Delete message)
        {
            var projection = GetProjection(message.Name);
            if (projection == null)
                message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
            else
                projection.Handle(message);
        }

        public void Handle(ProjectionManagementMessage.GetQuery message)
        {
            var projection = GetProjection(message.Name);
            if (projection == null)
                message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
            else
                projection.Handle(message);
        }

        public void Handle(ProjectionManagementMessage.UpdateQuery message)
        {
            _logger.Info(
                "Updating '{0}' projection source to '{1}' (Requested type is: '{2}')", message.Name, message.Query,
                message.HandlerType);
            var projection = GetProjection(message.Name);
            if (projection == null)
                message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
            else
                projection.Handle(message); // update query text
        }

        public void Handle(ProjectionManagementMessage.Disable message)
        {
            _logger.Info("Disabling '{0}' projection", message.Name);

            var projection = GetProjection(message.Name);
            if (projection == null)
                message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
            else
                projection.Handle(message);
        }

        public void Handle(ProjectionManagementMessage.Enable message)
        {
            _logger.Info("Enabling '{0}' projection", message.Name);

            var projection = GetProjection(message.Name);
            if (projection == null)
            {
                _logger.Error("DBG: PROJECTION *{0}* NOT FOUND!!!", message.Name);
                message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
            }
            else
                projection.Handle(message);
        }

        public void Handle(ProjectionManagementMessage.GetStatistics message)
        {
            if (!string.IsNullOrEmpty(message.Name))
            {
                var projection = GetProjection(message.Name);
                if (projection == null)
                    message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
                else
                    message.Envelope.ReplyWith(
                        new ProjectionManagementMessage.Statistics(
                            new[] {projection.GetStatistics()}));
            }
            else
            {
                var statuses = (from projectionNameValue in _projections
                                let projection = projectionNameValue.Value
                                where !projection.Deleted
                                where message.Mode == null || message.Mode == projection.GetMode()
                                let status = projection.GetStatistics()
                                select status).ToArray();
                message.Envelope.ReplyWith(
                    new ProjectionManagementMessage.Statistics(statuses));
            }
        }

        public void Handle(ProjectionManagementMessage.GetState message)
        {
            var projection = GetProjection(message.Name);
            if (projection == null)
                message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
            else
                projection.Handle(message);
        }

        public void Handle(ProjectionManagementMessage.GetDebugState message)
        {
            var projection = GetProjection(message.Name);
            if (projection == null)
                message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
            else
                projection.Handle(message);
        }

        public void Handle(CoreProjectionManagementMessage.Started message)
        {
            string name;
            if (_projectionsMap.TryGetValue(message.ProjectionId, out name))
            {
                var projection = _projections[name];
                projection.Handle(message);
            }
        }

        public void Handle(CoreProjectionManagementMessage.Stopped message)
        {
            string name;
            if (_projectionsMap.TryGetValue(message.ProjectionId, out name))
            {
                var projection = _projections[name];
                projection.Handle(message);
            }
        }

        public void Handle(CoreProjectionManagementMessage.Faulted message)
        {
            string name;
            if (_projectionsMap.TryGetValue(message.ProjectionId, out name))
            {
                var projection = _projections[name];
                projection.Handle(message);
            }
        }

        public void Handle(CoreProjectionManagementMessage.Prepared message)
        {
            string name;
            if (_projectionsMap.TryGetValue(message.ProjectionId, out name))
            {
                var projection = _projections[name];
                projection.Handle(message);
            }
        }

        public void Handle(CoreProjectionManagementMessage.StateReport message)
        {
            string name;
            if (_projectionsMap.TryGetValue(message.ProjectionId, out name))
            {
                var projection = _projections[name];
                projection.Handle(message);
            }
        }

        public void Handle(CoreProjectionManagementMessage.DebugState message)
        {
            string name;
            if (_projectionsMap.TryGetValue(message.ProjectionId, out name))
            {
                var projection = _projections[name];
                projection.Handle(message);
            }
        }

        public void Handle(CoreProjectionManagementMessage.StatisticsReport message)
        {
            string name;
            if (_projectionsMap.TryGetValue(message.ProjectionId, out name))
            {
                var projection = _projections[name];
                projection.Handle(message);
            }
        }

        public void Handle(ClientMessage.ReadStreamEventsBackwardCompleted message)
        {
            _readDispatcher.Handle(message);
        }

        public void Handle(ClientMessage.WriteEventsCompleted message)
        {
            _writeDispatcher.Handle(message);
        }

        public void Dispose()
        {
            foreach (var projection in _projections.Values)
                projection.Dispose();
            _projections.Clear();
        }

        private ManagedProjection GetProjection(string name)
        {
            ManagedProjection result;
            return _projections.TryGetValue(name, out result) ? result : null;
        }

        private void StartExistingProjections()
        {
            BeginLoadProjectionList();
        }

        private void BeginLoadProjectionList(int from = -1)
        {
            _readDispatcher.Publish(
                new ClientMessage.ReadStreamEventsBackward(
                    Guid.NewGuid(), _readDispatcher.Envelope, "$projections-$all", from, _readEventsBatchSize,
                    resolveLinks: false, validationStreamVersion: null), m => LoadProjectionListCompleted(m, from));
        }

        private void LoadProjectionListCompleted(
            ClientMessage.ReadStreamEventsBackwardCompleted completed, int requestedFrom)
        {
            if (completed.Result == StreamResult.Success && completed.Events.Length > 0)
            {
                if (completed.NextEventNumber != -1)
                    BeginLoadProjectionList(@from: completed.NextEventNumber);
                foreach (var @event in completed.Events.Where(v => v.Event.EventType == "ProjectionCreated"))
                {
                    var projectionName = Encoding.UTF8.GetString(@event.Event.Data);
                    if (_projections.ContainsKey(projectionName))
                    {
                        //TODO: log this event as it should not happen
                        continue; // ignore older attempts to create a projection
                    }
                    var managedProjection = CreateManagedProjectionInstance(projectionName);
                    managedProjection.InitializeExisting(projectionName);
                }
            }
            else
            {
                if (requestedFrom == -1)
                {
                    _logger.Info(
                        "Projection manager is initializing from the empty {0} stream", completed.EventStreamId);

                    CreatePredefinedProjections();
                }
            }
        }

        private void CreatePredefinedProjections()
        {
            CreatePredefinedProjection("$streams", typeof (IndexStreams), "");
            CreatePredefinedProjection("$stream_by_category", typeof(CategorizeStreamByPath), "-");
            CreatePredefinedProjection("$by_category", typeof(CategorizeEventsByStreamPath), "-");
            CreatePredefinedProjection("$by_event_type", typeof(IndexEventsByEventType), "");
        }

        private void CreatePredefinedProjection(string name, Type handlerType, string config)
        {
            IEnvelope envelope = new NoopEnvelope();

            var postMessage = new ProjectionManagementMessage.Post(
                envelope, ProjectionMode.Continuous, name, "native:" + handlerType.Namespace + "." + handlerType.Name,
                config, enabled: false, checkpointsEnabled: true, emitEnabled: true);

            _publisher.Publish(postMessage);
        }

        private void PostNewProjection(ProjectionManagementMessage.Post message, Action<ManagedProjection> completed)
        {
            if (message.Mode > ProjectionMode.OneTime)
            {
                BeginWriteProjectionRegistration(
                    message.Name, () =>
                        {
                            var projection = CreateManagedProjectionInstance(message.Name);
                            projection.InitializeNew(message, () => completed(projection));
                        });
            }
            else
            {
                var projection = CreateManagedProjectionInstance(message.Name);
                projection.InitializeNew(message, () => completed(projection));
            }
        }

        private int _lastUsedQueue = 0;
        private bool _started;

        private ManagedProjection CreateManagedProjectionInstance(string name)
        {
            var projectionCorrelationId = Guid.NewGuid();
            IPublisher queue;
            if (_lastUsedQueue >= _queues.Length)
                _lastUsedQueue = 0;
            queue = _queues[_lastUsedQueue];
            _lastUsedQueue++;

            var managedProjectionInstance = new ManagedProjection(queue, 
                projectionCorrelationId, name, _logger, _writeDispatcher, _readDispatcher, _inputQueue,
                _projectionStateHandlerFactory);
            _projectionsMap.Add(projectionCorrelationId, name);
            _projections.Add(name, managedProjectionInstance);
            return managedProjectionInstance;
        }

        private void BeginWriteProjectionRegistration(string name, Action completed)
        {
            _writeDispatcher.Publish(
                new ClientMessage.WriteEvents(
                    Guid.NewGuid(), _writeDispatcher.Envelope, true, "$projections-$all", ExpectedVersion.Any,
                    new Event(Guid.NewGuid(), "ProjectionCreated", false, Encoding.UTF8.GetBytes(name), new byte[0])),
                m => WriteProjectionRegistrationCompleted(m, completed, name));
        }

        private void WriteProjectionRegistrationCompleted(
            ClientMessage.WriteEventsCompleted message, Action completed, string name)
        {
            if (message.ErrorCode == OperationErrorCode.Success)
            {
                if (completed != null) completed();
                return;
            }
            _logger.Info(
                "Projection '{0}' registration has not been written to {1}. Error: {2}", name, message.EventStreamId,
                Enum.GetName(typeof (OperationErrorCode), message.ErrorCode));
            if (message.ErrorCode == OperationErrorCode.CommitTimeout
                || message.ErrorCode == OperationErrorCode.ForwardTimeout
                || message.ErrorCode == OperationErrorCode.PrepareTimeout
                || message.ErrorCode == OperationErrorCode.WrongExpectedVersion)
            {
                _logger.Info("Retrying write projection registration for {0}", name);
                BeginWriteProjectionRegistration(name, completed);
            }
            else
                throw new NotSupportedException("Unsupported error code received");
        }

        public void Handle(SystemMessage.StateChangeMessage message)
        {
            if (message.State == VNodeState.Master)
            {
                if (!_started)
                    Start();
            }
            else
            {
                if (_started)
                    Stop();
            }
        }
    }
}
