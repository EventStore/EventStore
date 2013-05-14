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
using EventStore.Core.Bus;
using EventStore.Core.DataStructures;
using EventStore.Core.Services.Monitoring.Stats;
using System.Threading.Tasks;

namespace EventStore.Core.Services.TimerService
{
    public class ThreadBasedScheduler : IMonitoredQueue, IScheduler, IDisposable
    {
        public string Name { get { return _queueStats.Name; } }

        private readonly Common.Concurrent.ConcurrentQueue<ScheduledTask> _pending = new Common.Concurrent.ConcurrentQueue<ScheduledTask>();
        private readonly PairingHeap<ScheduledTask> _tasks = new PairingHeap<ScheduledTask>((x, y) => x.DueTime < y.DueTime);

        private readonly ITimeProvider _timeProvider;

        private readonly Task _timerThread;
        private volatile bool _stop;

        private readonly QueueStatsCollector _queueStats = new QueueStatsCollector("Timer");

        public ThreadBasedScheduler(ITimeProvider timeProvider)
        {
            Ensure.NotNull(timeProvider, "timeProvider");
            _timeProvider = timeProvider;

            _timerThread = new Task(DoTiming,TaskCreationOptions.LongRunning);

            _timerThread.Start();
        }

        public void Schedule(TimeSpan after, Action<IScheduler, object> callback, object state)
        {
            _pending.Enqueue(new ScheduledTask(_timeProvider.Now.Add(after), callback, state));
        }

        private void DoTiming()
        {
            _queueStats.Start();
            QueueMonitor.Default.Register(this);

            while (!_stop)
            {
                _queueStats.EnterBusy();
                _queueStats.ProcessingStarted<SchedulePendingTasks>(_pending.Count);

                int pending = 0;
                ScheduledTask task;
                while (_pending.TryDequeue(out task))
                {
                    _tasks.Add(task);
                    pending += 1;
                }

                _queueStats.ProcessingEnded(pending);

                _queueStats.ProcessingStarted<ExecuteScheduledTasks>(_tasks.Count);
                int processed = 0;
                while (_tasks.Count > 0 && _tasks.FindMin().DueTime <= _timeProvider.Now)
                {
                    processed += 1;
                    var scheduledTask = _tasks.DeleteMin();
                    scheduledTask.Action(this, scheduledTask.State);
                }
                _queueStats.ProcessingEnded(processed);

                if (processed == 0)
                {
                    _queueStats.EnterIdle();
                    Thread.Sleep(1);
                }
            }

            _queueStats.Stop();
            QueueMonitor.Default.Unregister(this);
        }

        public void Dispose()
        {
            _stop = true;
        }

        public QueueStats GetStatistics()
        {
            return _queueStats.GetStatistics(_tasks.Count);
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

        private class SchedulePendingTasks
        {
        }

        private class ExecuteScheduledTasks
        {
        }
    }
}
