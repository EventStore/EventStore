using System;
using EventStore.Common.Options;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Utils;

namespace EventStore.Projections.Core.Services.Management
{
    public class ProjectionCoreCoordinator
        : IHandle<ProjectionManagementMessage.Internal.RegularTimeout>, IHandle<SystemMessage.StateChangeMessage>
    {
        private readonly ProjectionType _runProjections;
        private readonly TimeoutScheduler[] _timeoutSchedulers;
        private readonly IPublisher[] _queues;
        private bool _started;
        private readonly IPublisher _publisher;
        private readonly IEnvelope _publishEnvelope;

        public ProjectionCoreCoordinator(
            ProjectionType runProjections,
            TimeoutScheduler[] timeoutSchedulers,
            IPublisher[] queues,
            
            IPublisher publisher,
            IEnvelope publishEnvelope)
        {
            _runProjections = runProjections;
            _timeoutSchedulers = timeoutSchedulers;
            _queues = queues;
            _publisher = publisher;
            _publishEnvelope = publishEnvelope;
        }

        public void Handle(ProjectionManagementMessage.Internal.RegularTimeout message)
        {
            ScheduleRegularTimeout();
            for (var i = 0; i < _timeoutSchedulers.Length; i++)
                _timeoutSchedulers[i].Tick();
        }

        public void Handle(SystemMessage.StateChangeMessage message)
        {
            DebugLogger.Log();
            DebugLogger.Log();
            DebugLogger.Log();
            DebugLogger.Log("=======================" + message.State + "================");
            if (message.State == VNodeState.Master // || message.State == VNodeState.Clone
                || message.State == VNodeState.Slave)
            {
                if (!_started)
                {
                    DebugLogger.Log("*** STARTING PROJECTION CORE ***");
                    Start();
                }
            }
            else
            {
                if (_started)
                {
                    DebugLogger.Log("*** STOPPING PROJECTION CORE ***");
                    Stop();
                }
            }
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

        private void Start()
        {
            if (_started)
                throw new InvalidOperationException();
            _started = true;
            ScheduleRegularTimeout();
            foreach (var queue in _queues)
            {
                queue.Publish(new ReaderCoreServiceMessage.StartReader());
                if (_runProjections >= ProjectionType.System)
                    queue.Publish(new ProjectionCoreServiceMessage.StartCore());
            }
        }

        private void Stop()
        {
            if (_started)
            {
                _started = false;
                foreach (var queue in _queues)
                {
                    queue.Publish(new ProjectionCoreServiceMessage.StopCore());
                    if (_runProjections >= ProjectionType.System)
                        queue.Publish(new ReaderCoreServiceMessage.StopReader());
                }
            }
        }

        public void SetupMessaging(IBus bus)
        {
            bus.Subscribe<SystemMessage.StateChangeMessage>(this);
            if (_runProjections >= ProjectionType.System)
            {
                bus.Subscribe<ProjectionManagementMessage.Internal.RegularTimeout>(this);
            }
        }
    }
}