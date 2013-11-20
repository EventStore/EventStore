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
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.DataStructures;

namespace EventStore.Core.Services.TimerService
{
    public class TimerBasedScheduler : IDisposable, IScheduler
    {
        private readonly PairingHeap<ScheduledTask> _tasks = new PairingHeap<ScheduledTask>((x, y) => x.DueTime < y.DueTime);

        private readonly ITimeProvider _timeProvider;
        private readonly ITimer _timer;

        private readonly object _queueLock = new object();

        public TimerBasedScheduler(ITimer timer, ITimeProvider timeProvider)
        {
            Ensure.NotNull(timer, "timer");
            Ensure.NotNull(timeProvider, "timeProvider");

            _timer = timer;
            _timeProvider = timeProvider;
        }

        public void Stop()
        {
            Dispose();
        }

        public void Schedule(TimeSpan after, Action<IScheduler, object> callback, object state)
        {
            lock (_queueLock)
            {
                _tasks.Add(new ScheduledTask(_timeProvider.Now.Add(after), callback, state));
                ResetTimer();
            }
        }

        protected void ProcessOperations()
        {
            while (_tasks.Count > 0 && _tasks.FindMin().DueTime <= _timeProvider.Now)
            {
                var scheduledTask = _tasks.DeleteMin();
                scheduledTask.Action(this, scheduledTask.State);
            }
        }

        private void OnTimerFired()
        {
            lock (_queueLock)
            {
                ProcessOperations();
                ResetTimer();
            }
        }

        private void ResetTimer()
        {
            if (_tasks.Count > 0)
            {
                var tuple = _tasks.FindMin();
                _timer.FireIn((int) (tuple.DueTime - _timeProvider.Now).TotalMilliseconds, OnTimerFired);
            }
            else
            {
                _timer.FireIn(Timeout.Infinite, OnTimerFired);
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        private struct ScheduledTask
        {
            public readonly DateTime DueTime;
            public readonly Action<IScheduler, object> Action;
            public readonly object State;

            public ScheduledTask(DateTime dueTime, Action<IScheduler, object> action, object state)
            {
                DueTime = dueTime;
                Action = action;
                State = state;
            }
        }
    }
}