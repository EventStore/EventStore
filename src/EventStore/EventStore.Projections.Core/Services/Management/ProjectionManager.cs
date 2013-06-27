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
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Standard;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

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
                                     IHandle<ProjectionManagementMessage.GetResult>,
                                     IHandle<ProjectionManagementMessage.Disable>,
                                     IHandle<ProjectionManagementMessage.Enable>,
                                     IHandle<ProjectionManagementMessage.SetRunAs>,
                                     IHandle<ProjectionManagementMessage.Reset>,
                                     IHandle<ProjectionManagementMessage.Internal.CleanupExpired>,
                                     IHandle<ProjectionManagementMessage.Internal.RegularTimeout>,
                                     IHandle<ProjectionManagementMessage.Internal.Deleted>,
                                     IHandle<CoreProjectionManagementMessage.Started>,
                                     IHandle<CoreProjectionManagementMessage.Stopped>,
                                     IHandle<CoreProjectionManagementMessage.Faulted>,
                                     IHandle<CoreProjectionManagementMessage.Prepared>,
                                     IHandle<CoreProjectionManagementMessage.StateReport>,
                                     IHandle<CoreProjectionManagementMessage.ResultReport>,
                                     IHandle<CoreProjectionManagementMessage.StatisticsReport>, 
                                     IHandle<ProjectionManagementMessage.RegisterSystemProjection>
    {
        private readonly ILogger _logger = LogManager.GetLoggerFor<ProjectionManager>();

        private readonly IPublisher _inputQueue;
        private readonly IPublisher _publisher;
        private readonly IPublisher[] _queues;
        private readonly TimeoutScheduler[] _timeoutSchedulers;
        private readonly ITimeProvider _timeProvider;
        private readonly bool _runProjections;
        private readonly bool _initializeSystemProjections;
        private readonly ProjectionStateHandlerFactory _projectionStateHandlerFactory;
        private readonly Dictionary<string, ManagedProjection> _projections;
        private readonly Dictionary<Guid, string> _projectionsMap;

        private readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>
            _writeDispatcher;

        private readonly RequestResponseDispatcher<ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>
            _readDispatcher;

        private int _readEventsBatchSize = 100;

        private int _lastUsedQueue = 0;
        private bool _started;
        private readonly PublishEnvelope _publishEnvelope;

        public ProjectionManager(
            IPublisher inputQueue, IPublisher publisher, IPublisher[] queues, ITimeProvider timeProvider,
            bool runProjections, bool initializeSystemProjections = true)
        {
            if (inputQueue == null) throw new ArgumentNullException("inputQueue");
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (queues == null) throw new ArgumentNullException("queues");
            if (queues.Length == 0) throw new ArgumentException("At least one queue is required", "queues");

            _inputQueue = inputQueue;
            _publisher = publisher;
            _queues = queues;

            _timeoutSchedulers = CreateTimeoutSchedulers(queues);

            _timeProvider = timeProvider;
            _runProjections = runProjections;
            _initializeSystemProjections = initializeSystemProjections;

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
            _publishEnvelope = new PublishEnvelope(_inputQueue, crossThread: true);
        }

        private static TimeoutScheduler[] CreateTimeoutSchedulers(IPublisher[] queues)
        {
            var timeoutSchedulers = new TimeoutScheduler[queues.Length];
            for (var i = 0; i < timeoutSchedulers.Length; i++)
                timeoutSchedulers[i] = new TimeoutScheduler();
            return timeoutSchedulers;
        }

        private void Start()
        {
            foreach (var queue in _queues)
            {
                queue.Publish(new Messages.ReaderCoreServiceMessage.StartReader());
                if (_runProjections)
                    queue.Publish(new ProjectionCoreServiceMessage.StartCore());
            }
            if (_runProjections)
                StartExistingProjections(
                    () =>
                        {
                            _started = true;
                            ScheduleExpire();
                            ScheduleRegularTimeout();
                        });
        }

        private void ScheduleExpire()
        {
            if (!_started)
                return;
            _publisher.Publish(
                TimerMessage.Schedule.Create(
                    TimeSpan.FromSeconds(60), _publishEnvelope, new ProjectionManagementMessage.Internal.CleanupExpired()));
        }

        private void ScheduleRegularTimeout()
        {
            if (!_started)
                return;
            _publisher.Publish(
                TimerMessage.Schedule.Create(
                    TimeSpan.FromMilliseconds(100), _publishEnvelope, new ProjectionManagementMessage.Internal.RegularTimeout()));
        }

        private void Stop()
        {
            _started = false;
            foreach (var queue in _queues)
            {
                queue.Publish(new ProjectionCoreServiceMessage.StopCore());
                if (_runProjections)
                    queue.Publish(new Messages.ReaderCoreServiceMessage.StopReader());
            }

            _writeDispatcher.CancelAll();
            _readDispatcher.CancelAll();

            _projections.Clear();
            _projectionsMap.Clear();
        }

        public void Handle(ProjectionManagementMessage.Post message)
        {
            if (!_started)
                return;

            if (
                !ProjectionManagementMessage.RunAs.ValidateRunAs(
                    message.Mode, ReadWrite.Write, null, message, replace: message.EnableRunAs)) return;

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
            if (!_started)
                return;
            var projection = GetProjection(message.Name);
            if (projection == null)
                message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
            else
                projection.Handle(message);
        }

        public void Handle(ProjectionManagementMessage.GetQuery message)
        {
            if (!_started)
                return;
            var projection = GetProjection(message.Name);
            if (projection == null)
                message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
            else
                projection.Handle(message);
        }

        public void Handle(ProjectionManagementMessage.UpdateQuery message)
        {
            if (!_started)
                return;
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
            if (!_started)
                return;
            _logger.Info("Disabling '{0}' projection", message.Name);

            var projection = GetProjection(message.Name);
            if (projection == null)
                message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
            else
                projection.Handle(message);
        }

        public void Handle(ProjectionManagementMessage.Enable message)
        {
            if (!_started)
                return;
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

        public void Handle(ProjectionManagementMessage.SetRunAs message)
        {
            if (!_started)
                return;
            _logger.Info("Setting RunAs account for '{0}' projection", message.Name);

            var projection = GetProjection(message.Name);
            if (projection == null)
            {
                _logger.Error("DBG: PROJECTION *{0}* NOT FOUND!!!", message.Name);
                message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
            }
            else
                projection.Handle(message);
        }

        public void Handle(ProjectionManagementMessage.Reset message)
        {
            if (!_started)
                return;
            _logger.Info("Resetting '{0}' projection", message.Name);

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
            if (!_started)
                return;
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
                                where 
                                    message.Mode == null || 
                                    message.Mode == projection.GetMode() || 
                                    (message.Mode.GetValueOrDefault() == ProjectionMode.AllNonTransient 
                                        && projection.GetMode() != ProjectionMode.Transient)
                                let status = projection.GetStatistics()
                                select status).ToArray();
                message.Envelope.ReplyWith(
                    new ProjectionManagementMessage.Statistics(statuses));
            }
        }

        public void Handle(ProjectionManagementMessage.GetState message)
        {
            if (!_started)
                return;
            var projection = GetProjection(message.Name);
            if (projection == null)
                message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
            else
                projection.Handle(message);
        }

        public void Handle(ProjectionManagementMessage.GetResult message)
        {
            if (!_started)
                return;
            var projection = GetProjection(message.Name);
            if (projection == null)
                message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
            else
                projection.Handle(message);
        }

        public void Handle(ProjectionManagementMessage.Internal.CleanupExpired message)
        {
            ScheduleExpire();
            CleanupExpired();
        }

        public void Handle(ProjectionManagementMessage.Internal.RegularTimeout message)
        {
            ScheduleRegularTimeout();
            for (var i = 0; i < _timeoutSchedulers.Length; i++)
                _timeoutSchedulers[i].Tick();
        }

        private void CleanupExpired()
        {
            foreach (var managedProjection in _projections.ToArray())
            {
                managedProjection.Value.Handle(new ProjectionManagementMessage.Internal.CleanupExpired());
            }
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

        public void Handle(CoreProjectionManagementMessage.ResultReport message)
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

        public void Handle(ProjectionManagementMessage.Internal.Deleted message)
        {
            _projections.Remove(message.Name);
            _projectionsMap.Remove(message.Id);
        }

        public void Handle(ProjectionManagementMessage.RegisterSystemProjection message)
        {
            if (!_projections.ContainsKey(message.Name))
            {
                Handle(
                    new ProjectionManagementMessage.Post(
                        new PublishEnvelope(_inputQueue), ProjectionMode.Continuous, message.Name,
                        ProjectionManagementMessage.RunAs.System, message.Handler, message.Query, true, true, true,
                        enableRunAs: true));
            }
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

        private void StartExistingProjections(Action completed)
        {
            BeginLoadProjectionList(completed);
        }

        private void BeginLoadProjectionList(Action completedAction, int from = -1)
        {
            var corrId = Guid.NewGuid();
            _readDispatcher.Publish(
                new ClientMessage.ReadStreamEventsBackward(
                    corrId, corrId, _readDispatcher.Envelope, "$projections-$all", from, _readEventsBatchSize,
                    resolveLinkTos: false, requireMaster: false, validationStreamVersion: null, user: SystemAccount.Principal), 
                m => LoadProjectionListCompleted(m, from, completedAction));
        }

        private void LoadProjectionListCompleted(
            ClientMessage.ReadStreamEventsBackwardCompleted completed, int requestedFrom, Action completedAction)
        {
            var anyFound = false;
            if (completed.Result == ReadStreamResult.Success)
            {
                var projectionRegistrations = completed.Events.Where(e => e.Event.EventType == "$ProjectionCreated").ToArray();
                if (projectionRegistrations.IsNotEmpty())
                    foreach (var @event in projectionRegistrations)
                    {
                        anyFound = true;
                        var projectionName = Helper.UTF8NoBom.GetString(@event.Event.Data);
                        if (string.IsNullOrEmpty(projectionName)
                            // NOTE: workaround for a bug allowing to create such projections
                            || _projections.ContainsKey(projectionName))
                        {
                            //TODO: log this event as it should not happen
                            continue; // ignore older attempts to create a projection
                        }
                        var projectionId = @event.Event.EventNumber;
                        //NOTE: fixing 0 projection problem
                        if (projectionId == 0)
                            projectionId = Int32.MaxValue - 1;
                        var managedProjection = CreateManagedProjectionInstance(
                            projectionName, projectionId);
                        managedProjection.InitializeExisting(projectionName);
                    }
            }
            if (requestedFrom == -1) // first chunk
            {
                if (!anyFound)
                {
                    _logger.Info(
                        "Projection manager is initializing from the empty {0} stream", completed.EventStreamId);
                    if ((completed.Result == ReadStreamResult.Success || completed.Result == ReadStreamResult.NoStream)
                        && completed.Events.Length == 0)
                    {
                        CreateFakeProjection(
                            () =>
                                {
                                    completedAction();
                                    CreateSystemProjections();
                                    RequestSystemProjections();
                                });
                        return;
                    }
                }
            }

            if (completed.Result == ReadStreamResult.Success && !completed.IsEndOfStream)
            {
                BeginLoadProjectionList(completedAction, @from: completed.NextEventNumber);
                return;
            }
            completedAction();
            RequestSystemProjections();
        }

        private void RequestSystemProjections()
        {
            _publisher.Publish(new ProjectionManagementMessage.RequestSystemProjections(new PublishEnvelope(_inputQueue)));
        }

        private void CreateFakeProjection(Action action)
        {
            var corrId = Guid.NewGuid();
            _writeDispatcher.Publish(
                new ClientMessage.WriteEvents(
                    corrId, corrId, _writeDispatcher.Envelope, true, "$projections-$all", ExpectedVersion.NoStream,
                    new Event(Guid.NewGuid(), "$ProjectionsInitialized", false, Empty.ByteArray, Empty.ByteArray),
                    SystemAccount.Principal), 
                completed => WriteFakeProjectionCompleted(completed, action));
        }

        private void WriteFakeProjectionCompleted(ClientMessage.WriteEventsCompleted completed, Action action)
        {
            switch (completed.Result)
            {
                case OperationResult.Success:
                    action();
                    break;
                case OperationResult.CommitTimeout:
                case OperationResult.ForwardTimeout:
                case OperationResult.PrepareTimeout:
                    CreateFakeProjection(action);
                    break;
                default:
                    _logger.Fatal("Cannot initialize projections subsystem. Cannot write a fake projection");
                    break;
            }

        }

        private void CreateSystemProjections()
        {
            if (_initializeSystemProjections)
            {
                CreateSystemProjection("$streams", typeof (IndexStreams), "");
                CreateSystemProjection("$stream_by_category", typeof (CategorizeStreamByPath), "-");
                CreateSystemProjection("$by_category", typeof (CategorizeEventsByStreamPath), "-");
                CreateSystemProjection("$by_event_type", typeof (IndexEventsByEventType), "");
            }
        }

        private void CreateSystemProjection(string name, Type handlerType, string config)
        {
            IEnvelope envelope = new NoopEnvelope();

            var postMessage = new ProjectionManagementMessage.Post(
                envelope, ProjectionMode.Continuous, name, ProjectionManagementMessage.RunAs.System, "native:" + handlerType.Namespace + "." + handlerType.Name,
                config, enabled: false, checkpointsEnabled: true, emitEnabled: true, enableRunAs: true);

            _publisher.Publish(postMessage);
        }

        private void PostNewProjection(ProjectionManagementMessage.Post message, Action<ManagedProjection> completed)
        {
            if (message.Mode >= ProjectionMode.OneTime)
            {
                BeginWriteProjectionRegistration(
                    message.Name, projectionId =>
                        {
                            var projection = CreateManagedProjectionInstance(message.Name, projectionId);
                            projection.InitializeNew(message, () => completed(projection));
                        });
            }
            else
            {
                var projection = CreateManagedProjectionInstance(message.Name, -1);
                projection.InitializeNew(message, () => completed(projection));
            }
        }

        private ManagedProjection CreateManagedProjectionInstance(string name, int projectionId)
        {
            var projectionCorrelationId = Guid.NewGuid();
            if (_lastUsedQueue >= _queues.Length)
                _lastUsedQueue = 0;
            var queueIndex = _lastUsedQueue;
            var queue = _queues[queueIndex];
            _lastUsedQueue++;

            var managedProjectionInstance = new ManagedProjection(queue, 
                projectionCorrelationId, projectionId, name, _logger, _writeDispatcher, _readDispatcher, _inputQueue, _publisher,
                _projectionStateHandlerFactory, _timeProvider, _timeoutSchedulers[queueIndex]);
            _projectionsMap.Add(projectionCorrelationId, name);
            _projections.Add(name, managedProjectionInstance);
            return managedProjectionInstance;
        }

        private void BeginWriteProjectionRegistration(string name, Action<int> completed)
        {
            const string eventStreamId = "$projections-$all";
            var corrId = Guid.NewGuid();
            _writeDispatcher.Publish(
                new ClientMessage.WriteEvents(
                    corrId, corrId, _writeDispatcher.Envelope, true, eventStreamId, ExpectedVersion.Any,
                    new Event(Guid.NewGuid(), "$ProjectionCreated", false, Helper.UTF8NoBom.GetBytes(name), Empty.ByteArray),
                    SystemAccount.Principal),
                m => WriteProjectionRegistrationCompleted(m, completed, name, eventStreamId));
        }

        private void WriteProjectionRegistrationCompleted(
            ClientMessage.WriteEventsCompleted message, Action<int> completed, string name, string eventStreamId)
        {
            if (message.Result == OperationResult.Success)
            {
                if (completed != null) completed(message.FirstEventNumber);
                return;
            }
            _logger.Info(
                "Projection '{0}' registration has not been written to {1}. Error: {2}", name, eventStreamId,
                Enum.GetName(typeof (OperationResult), message.Result));
            if (message.Result == OperationResult.CommitTimeout
                || message.Result == OperationResult.ForwardTimeout
                || message.Result == OperationResult.PrepareTimeout
                || message.Result == OperationResult.WrongExpectedVersion)
            {
                _logger.Info("Retrying write projection registration for {0}", name);
                BeginWriteProjectionRegistration(name, completed);
            }
            else
                throw new NotSupportedException("Unsupported error code received");
        }

    }
}
