using System;
using EventStore.Common.Options;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Management
{
    public class ProjectionCoreCoordinator
        : IHandle<ProjectionManagementMessage.Internal.RegularTimeout>, IHandle<SystemMessage.StateChangeMessage>
    {
        private readonly RunProjections _runProjections;
        private readonly TimeoutScheduler[] _timeoutSchedulers;
        private bool _started;
        private readonly IPublisher _publisher;
        private readonly IEnvelope _publishEnvelope;

        public ProjectionCoreCoordinator(
            RunProjections runProjections,
            TimeoutScheduler[] timeoutSchedulers,
            IPublisher publisher,
            IEnvelope publishEnvelope)
        {
            _runProjections = runProjections;
            _timeoutSchedulers = timeoutSchedulers;
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
            if (message.State == VNodeState.Master || message.State == VNodeState.Clone
                || message.State == VNodeState.Slave)
            {
                if (!_started)
                {
                    Start();
                }
            }
            else
            {
                if (_started)
                {
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
        }

        private void Stop()
        {
            if (_started)
            {
                _started = false;
            }
        }

        public void SetupMessaging(IBus bus)
        {
            bus.Subscribe<SystemMessage.StateChangeMessage>(this);
            if (_runProjections >= RunProjections.System)
            {
                bus.Subscribe<ProjectionManagementMessage.Internal.RegularTimeout>(this);
            }
        }
    }
}