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
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Standard;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Projections.Core.Services.Management
{
    public class ProjectionManager
        : IDisposable,
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
            IHandle<ProjectionManagementMessage.Abort>,
            IHandle<ProjectionManagementMessage.SetRunAs>,
            IHandle<ProjectionManagementMessage.Reset>,
            IHandle<ProjectionManagementMessage.StartSlaveProjections>,
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
            IHandle<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>,
            IHandle<ProjectionManagementMessage.RegisterSystemProjection>

    {

        public const int ProjectionQueryId = -2;

        private readonly ILogger _logger = LogManager.GetLoggerFor<ProjectionManager>();

        private readonly IPublisher _inputQueue;
        private readonly IPublisher _publisher;
        private readonly Tuple<Guid, IPublisher>[] _queues;
        private readonly IDictionary<Guid, IPublisher> _queueMap;
        private readonly TimeoutScheduler[] _timeoutSchedulers;
        private readonly ITimeProvider _timeProvider;
        private readonly RunProjections _runProjections;
        private readonly bool _initializeSystemProjections;
        private readonly Dictionary<string, ManagedProjection> _projections;
        private readonly Dictionary<Guid, string> _projectionsMap;

        private readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>
            _writeDispatcher;

        private readonly
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>
            _readDispatcher;

        private int _readEventsBatchSize = 100;

        private int _lastUsedQueue = 0;
        private bool _started;
        private readonly PublishEnvelope _publishEnvelope;

        public ProjectionManager(
            IPublisher inputQueue,
            IPublisher publisher,
            IDictionary<Guid, IPublisher> queueMap,
            ITimeProvider timeProvider,
            RunProjections runProjections,
            TimeoutScheduler[] timeoutSchedulers,
            bool initializeSystemProjections = true)
        {
            if (inputQueue == null) throw new ArgumentNullException("inputQueue");
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (queueMap == null) throw new ArgumentNullException("queueMap");
            if (queueMap.Count == 0) throw new ArgumentException("At least one queue is required", "queueMap");

            _inputQueue = inputQueue;
            _publisher = publisher;
            _queues = queueMap.Select(v => Tuple.Create(v.Key, v.Value)).ToArray();

            _timeoutSchedulers = timeoutSchedulers;

            _timeProvider = timeProvider;
            _runProjections = runProjections;
            _initializeSystemProjections = initializeSystemProjections;

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


            _projections = new Dictionary<string, ManagedProjection>();
            _projectionsMap = new Dictionary<Guid, string>();
            _publishEnvelope = new PublishEnvelope(_inputQueue, crossThread: true);
        }

        private void Start()
        {
            _publisher.Publish(new ProjectionManagementMessage.Starting());
            foreach (var queue in _queues)
            {
                var queuePublisher = queue.Item2;
                queuePublisher.Publish(new Messages.ReaderCoreServiceMessage.StartReader());
                if (_runProjections >= RunProjections.System)
                    queuePublisher.Publish(new ProjectionCoreServiceMessage.StartCore());
            }
            if (_runProjections >= RunProjections.System)
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
                    TimeSpan.FromSeconds(60),
                    _publishEnvelope,
                    new ProjectionManagementMessage.Internal.CleanupExpired()));
        }

        private void ScheduleRegularTimeout()
        {
            if (!_started)
                return;
            _publisher.Publish(
                TimerMessage.Schedule.Create(
                    TimeSpan.FromMilliseconds(100),
                    _publishEnvelope,
                    new ProjectionManagementMessage.Internal.RegularTimeout()));
        }

        private void Stop()
        {
            _started = false;
            foreach (var queue in _queues)
            {
                var queuePublisher = queue.Item2;
                queuePublisher.Publish(new ProjectionCoreServiceMessage.StopCore());
                if (_runProjections >= RunProjections.System)
                    queuePublisher.Publish(new Messages.ReaderCoreServiceMessage.StopReader());
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
                    message.Mode,
                    ReadWrite.Write,
                    null,
                    message,
                    replace: message.EnableRunAs)) return;

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
                        new ProjectionManagementMessage.Conflict("Duplicate projection name: " + message.Name));
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
            {
                projection.Handle(message);
                _projections.Remove(message.Name);
                _projectionsMap.Remove(projection.Id);
            }
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
                "Updating '{0}' projection source to '{1}' (Requested type is: '{2}')",
                message.Name,
                message.Query,
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

        public void Handle(ProjectionManagementMessage.Abort message)
        {
            if (!_started)
                return;
            _logger.Info("Aborting '{0}' projection", message.Name);

            var projection = GetProjection(message.Name);
            if (projection == null)
                message.Envelope.ReplyWith(new ProjectionManagementMessage.NotFound());
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
                        new ProjectionManagementMessage.Statistics(new[] {projection.GetStatistics()}));
            }
            else
            {
                var statuses = (from projectionNameValue in _projections
                    let projection = projectionNameValue.Value
                    where !projection.Deleted
                    where
                        message.Mode == null || message.Mode == projection.GetMode()
                        || (message.Mode.GetValueOrDefault() == ProjectionMode.AllNonTransient
                            && projection.GetMode() != ProjectionMode.Transient)
                    let status = projection.GetStatistics()
                    select status).ToArray();
                message.Envelope.ReplyWith(new ProjectionManagementMessage.Statistics(statuses));
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

        public void Handle(CoreProjectionManagementMessage.SlaveProjectionReaderAssigned message)
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
                        new PublishEnvelope(_inputQueue),
                        ProjectionMode.Continuous,
                        message.Name,
                        ProjectionManagementMessage.RunAs.System,
                        message.Handler,
                        message.Query,
                        true,
                        true,
                        true,
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
                    corrId,
                    corrId,
                    _readDispatcher.Envelope,
                    "$projections-$all",
                    from,
                    _readEventsBatchSize,
                    resolveLinkTos: false,
                    requireMaster: false,
                    validationStreamVersion: null,
                    user: SystemAccount.Principal),
                m => LoadProjectionListCompleted(m, from, completedAction));
        }

        private void LoadProjectionListCompleted(
            ClientMessage.ReadStreamEventsBackwardCompleted completed,
            int requestedFrom,
            Action completedAction)
        {
            var anyFound = false;
            if (completed.Result == ReadStreamResult.Success)
            {
                var projectionRegistrations =
                    completed.Events.Where(e => e.Event.EventType == "$ProjectionCreated").ToArray();
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
                        var enabledToRun = IsProjectionEnabledToRunByMode(projectionName);
                        var managedProjection = CreateManagedProjectionInstance(
                            projectionName,
                            projectionId,
                            Guid.NewGuid(),
                            GetNextQueueIndex());
                        managedProjection.InitializeExisting(projectionName);
                    }
            }
            if (requestedFrom == -1) // first chunk
            {
                if (!anyFound)
                {
                    _logger.Info(
                        "Projection manager is initializing from the empty {0} stream",
                        completed.EventStreamId);
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

        private bool IsProjectionEnabledToRunByMode(string projectionName)
        {
            return _runProjections >= RunProjections.All
                   || _runProjections == RunProjections.System && projectionName.StartsWith("$");
        }

        private void RequestSystemProjections()
        {
            _publisher.Publish(
                new ProjectionManagementMessage.RequestSystemProjections(new PublishEnvelope(_inputQueue)));
        }

        private void CreateFakeProjection(Action action)
        {
            var corrId = Guid.NewGuid();
            _writeDispatcher.Publish(
                new ClientMessage.WriteEvents(
                    corrId,
                    corrId,
                    _writeDispatcher.Envelope,
                    true,
                    "$projections-$all",
                    ExpectedVersion.NoStream,
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
                CreateSystemProjection(
                    ProjectionNamesBuilder.StandardProjections.StreamsStandardProjection,
                    typeof (IndexStreams),
                    "");
                CreateSystemProjection(
                    ProjectionNamesBuilder.StandardProjections.StreamByCategoryStandardProjection,
                    typeof (CategorizeStreamByPath),
                    "first\r\n-");
                CreateSystemProjection(
                    ProjectionNamesBuilder.StandardProjections.EventByCategoryStandardProjection,
                    typeof (CategorizeEventsByStreamPath),
                    "first\r\n-");
                CreateSystemProjection(
                    ProjectionNamesBuilder.StandardProjections.EventByTypeStandardProjection,
                    typeof (IndexEventsByEventType),
                    "");
            }
        }

        private void CreateSystemProjection(string name, Type handlerType, string config)
        {
            IEnvelope envelope = new NoopEnvelope();

            var postMessage = new ProjectionManagementMessage.Post(
                envelope,
                ProjectionMode.Continuous,
                name,
                ProjectionManagementMessage.RunAs.System,
                "native:" + handlerType.Namespace + "." + handlerType.Name,
                config,
                enabled: false,
                checkpointsEnabled: true,
                emitEnabled: true,
                enableRunAs: true);

            _publisher.Publish(postMessage);
        }

        private void PostNewProjection(ProjectionManagementMessage.Post message, Action<ManagedProjection> completed)
        {
            if (message.Mode >= ProjectionMode.OneTime)
            {
                BeginWriteProjectionRegistration(
                    message.Name,
                    projectionId =>
                        new ProjectionManager.NewProjectionInitializer(
                            projectionId,
                            message.Name,
                            message.Mode,
                            message.HandlerType,
                            message.Query,
                            message.Enabled,
                            message.EmitEnabled,
                            message.CheckpointsEnabled,
                            message.EnableRunAs,
                            message.RunAs).CreateAndInitializeNewProjection(
                                this,
                                completed,
                                Guid.NewGuid(),
                                GetNextQueueIndex()));
            }
            else
                new ProjectionManager.NewProjectionInitializer(
                    ProjectionQueryId,
                    message.Name,
                    message.Mode,
                    message.HandlerType,
                    message.Query,
                    message.Enabled,
                    message.EmitEnabled,
                    message.CheckpointsEnabled,
                    message.EnableRunAs,
                    message.RunAs).CreateAndInitializeNewProjection(
                        this,
                        completed,
                        Guid.NewGuid(),
                        GetNextQueueIndex());
        }

        public class NewProjectionInitializer
        {
            private readonly int _projectionId;
            private readonly bool _enabled;
            private readonly string _handlerType;
            private readonly string _query;
            private readonly ProjectionMode _projectionMode;
            private readonly bool _emitEnabled;
            private readonly bool _checkpointsEnabled;
            private readonly bool _enableRunAs;
            private readonly ProjectionManagementMessage.RunAs _runAs;
            private readonly string _name;

            public NewProjectionInitializer(
                int projectionId,
                string name,
                ProjectionMode projectionMode,
                string handlerType,
                string query,
                bool enabled,
                bool emitEnabled,
                bool checkpointsEnabled,
                bool enableRunAs,
                ProjectionManagementMessage.RunAs runAs)
            {
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
                _enableRunAs = enableRunAs;
                _runAs = runAs;
                _name = name;
            }

            public void CreateAndInitializeNewProjection(
                ProjectionManager projectionManager,
                Action<ManagedProjection> completed,
                Guid projectionCorrelationId,
                int queueIndex,
                bool isSlave = false,
                Guid slaveMasterWorkerId = default(Guid),
                IPublisher slaveResultsPublisher = null,
                Guid slaveMasterCorrelationId = default(Guid))
            {
                var projection = projectionManager.CreateManagedProjectionInstance(
                    _name,
                    _projectionId,
                    projectionCorrelationId,
                    queueIndex,
                    isSlave,
                    slaveMasterWorkerId,
                    slaveMasterCorrelationId);
                projection.InitializeNew(
                    () => completed(projection),
                    new ManagedProjection.PersistedState
                    {
                        Enabled = _enabled,
                        HandlerType = _handlerType,
                        Query = _query,
                        Mode = _projectionMode,
                        EmitEnabled = _emitEnabled,
                        CheckpointsDisabled = !_checkpointsEnabled,
                        Epoch = -1,
                        Version = -1,
                        RunAs = _enableRunAs ? ManagedProjection.SerializePrincipal(_runAs) : null,
                    });
            }
        }

        private ManagedProjection CreateManagedProjectionInstance(
            string name,
            int projectionId,
            Guid projectionCorrelationId,
            int queueIndex,
            bool isSlave = false,
            Guid slaveMasterWorkerId = default(Guid),
            Guid slaveMasterCorrelationId = default(Guid))
        {
            var queue = _queues[queueIndex];
            _lastUsedQueue++;
            var enabledToRun = IsProjectionEnabledToRunByMode(name);
            var workerId = queue.Item1;
            var queuePublisher = queue.Item2;
            var managedProjectionInstance = new ManagedProjection(
                workerId,
                projectionCorrelationId,
                projectionId,
                name,
                enabledToRun,
                _logger,
                _writeDispatcher,
                _readDispatcher,
                _inputQueue,
                _publisher,
                _timeProvider,
                _timeoutSchedulers[queueIndex],
                isSlave,
                slaveMasterWorkerId,
                slaveMasterCorrelationId);
            _projectionsMap.Add(projectionCorrelationId, name);
            _projections.Add(name, managedProjectionInstance);
            return managedProjectionInstance;
        }

        private int GetNextQueueIndex()
        {
            int queueIndex;
            if (_lastUsedQueue >= _queues.Length)
                _lastUsedQueue = 0;
            queueIndex = _lastUsedQueue;
            return queueIndex;
        }

        private void BeginWriteProjectionRegistration(string name, Action<int> completed)
        {
            const string eventStreamId = "$projections-$all";
            var corrId = Guid.NewGuid();
            _writeDispatcher.Publish(
                new ClientMessage.WriteEvents(
                    corrId,
                    corrId,
                    _writeDispatcher.Envelope,
                    true,
                    eventStreamId,
                    ExpectedVersion.Any,
                    new Event(
                        Guid.NewGuid(),
                        "$ProjectionCreated",
                        false,
                        Helper.UTF8NoBom.GetBytes(name),
                        Empty.ByteArray),
                    SystemAccount.Principal),
                m => WriteProjectionRegistrationCompleted(m, completed, name, eventStreamId));
        }

        private void WriteProjectionRegistrationCompleted(
            ClientMessage.WriteEventsCompleted message,
            Action<int> completed,
            string name,
            string eventStreamId)
        {
            if (message.Result == OperationResult.Success)
            {
                if (completed != null) completed(message.FirstEventNumber);
                return;
            }
            _logger.Info(
                "Projection '{0}' registration has not been written to {1}. Error: {2}",
                name,
                eventStreamId,
                Enum.GetName(typeof (OperationResult), message.Result));
            if (message.Result == OperationResult.CommitTimeout || message.Result == OperationResult.ForwardTimeout
                || message.Result == OperationResult.PrepareTimeout
                || message.Result == OperationResult.WrongExpectedVersion)
            {
                _logger.Info("Retrying write projection registration for {0}", name);
                BeginWriteProjectionRegistration(name, completed);
            }
            else
                throw new NotSupportedException("Unsupported error code received");
        }

        public void Handle(ProjectionManagementMessage.StartSlaveProjections message)
        {
            var result = new Dictionary<string, SlaveProjectionCommunicationChannel[]>();
            var counter = 0;
            foreach (var g in message.SlaveProjections.Definitions)
            {
                var @group = g;
                switch (g.RequestedNumber)
                {
                    case SlaveProjectionDefinitions.SlaveProjectionRequestedNumber.One:
                    case SlaveProjectionDefinitions.SlaveProjectionRequestedNumber.OnePerNode:
                    {
                        var resultArray = new SlaveProjectionCommunicationChannel[1];
                        result.Add(g.Name, resultArray);
                        counter++;
                        int queueIndex = GetNextQueueIndex();
                        CINP(
                            message,
                            @group,
                            resultArray,
                            queueIndex,
                            0,
                            () => CheckSlaveProjectionsStarted(message, ref counter, result));
                        break;
                    }
                    case SlaveProjectionDefinitions.SlaveProjectionRequestedNumber.OnePerThread:
                    {
                        var resultArray = new SlaveProjectionCommunicationChannel[_queues.Length];
                        result.Add(g.Name, resultArray);

                        for (int index = 0; index < _queues.Length; index++)
                        {
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
            ProjectionManagementMessage.StartSlaveProjections message,
            ref int counter,
            Dictionary<string, SlaveProjectionCommunicationChannel[]> result)
        {
            counter--;
            if (counter == 0)
                message.Envelope.ReplyWith(
                    new ProjectionManagementMessage.SlaveProjectionsStarted(
                        message.MasterCorrelationId,
                        new SlaveProjectionCommunicationChannels(result)));
        }

        private void CINP(
            ProjectionManagementMessage.StartSlaveProjections message,
            SlaveProjectionDefinitions.Definition @group,
            SlaveProjectionCommunicationChannel[] resultArray,
            int queueIndex,
            int arrayIndex,
            Action completed)
        {
            var projectionCorrelationId = Guid.NewGuid();
            var slaveProjectionName = message.Name + "-" + @group.Name + "-" + queueIndex;
            var initializer = new ProjectionManager.NewProjectionInitializer(
                ProjectionQueryId,
                slaveProjectionName,
                @group.Mode,
                @group.HandlerType,
                @group.Query,
                true,
                @group.EmitEnabled,
                @group.CheckpointsEnabled,
                @group.EnableRunAs,
                @group.RunAs);
            initializer.CreateAndInitializeNewProjection(
                this,
                managedProjection =>
                {
                    var queuePublisher = _queues[queueIndex].Item2;
                    var queueWorkerId = _queues[queueIndex].Item1;

                    resultArray[arrayIndex] = new SlaveProjectionCommunicationChannel(
                        slaveProjectionName,
                        queueWorkerId,
                        projectionCorrelationId,
                        managedProjection.SlaveProjectionSubscriptionId,
                        queuePublisher);
                    completed();
                },
                projectionCorrelationId,
                queueIndex,
                true,
                message.MasterWorkerId,
                message.ResultsPublisher,
                message.MasterCorrelationId);
        }

    }
}
