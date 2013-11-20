﻿// Copyright (c) 2012, Event Store LLP
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
using System.Diagnostics;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Services.Monitoring.Stats;

namespace EventStore.Core.Bus
{
    public class QueueStatsCollector
    {
        private static readonly TimeSpan MinRefreshPeriod = TimeSpan.FromMilliseconds(100);

        public readonly string Name;
        public readonly string GroupName;

        public Type InProgressMessage { get { return _inProgressMsgType; } }

        private readonly object _statisticsLock = new object(); // this lock is mostly acquired from a single thread (+ rarely to get statistics), so performance penalty is not too high
        
        private readonly Stopwatch _busyWatch = new Stopwatch();
        private readonly Stopwatch _idleWatch = new Stopwatch();
        private readonly Stopwatch _totalIdleWatch = new Stopwatch();
        private readonly Stopwatch _totalBusyWatch = new Stopwatch();
        private readonly Stopwatch _totalTimeWatch = new Stopwatch();
        private TimeSpan _lastTotalIdleTime;
        private TimeSpan _lastTotalBusyTime;
        private TimeSpan _lastTotalTime;

        private long _totalItems;
        private long _lastTotalItems;
        private int _lifetimeQueueLengthPeak;
        private int _currentQueueLengthPeak;
        private Type _lastProcessedMsgType;
        private Type _inProgressMsgType;

        private bool _wasIdle;

        public QueueStatsCollector(string name, string groupName = null)
        {
            Ensure.NotNull(name, "name");

            Name = name;
            GroupName = groupName;
        }

        public void Start()
        {
            _totalTimeWatch.Start();
            EnterIdle();
        }

        public void Stop()
        {
            EnterIdle();
            _totalTimeWatch.Stop();
        }

        public void ProcessingStarted<T>(int queueLength)
        {
            ProcessingStarted(typeof(T), queueLength);
        }

        public void ProcessingStarted(Type msgType, int queueLength)
        {
            _lifetimeQueueLengthPeak = _lifetimeQueueLengthPeak > queueLength ? _lifetimeQueueLengthPeak : queueLength;
            _currentQueueLengthPeak = _currentQueueLengthPeak > queueLength ? _currentQueueLengthPeak : queueLength;

            _inProgressMsgType = msgType;
        }

        public void ProcessingEnded(int itemsProcessed)
        {
            Interlocked.Add(ref _totalItems, itemsProcessed);
            _lastProcessedMsgType = _inProgressMsgType;
            _inProgressMsgType = null;
        }

        public void EnterIdle()
        {
            if (_wasIdle)
                return;
            _wasIdle = true;

            //NOTE: the following locks are primarily acquired in main thread, 
            //      so not too high performance penalty
            lock (_statisticsLock)
            {
                _totalIdleWatch.Start();
                _idleWatch.Restart();

                _totalBusyWatch.Stop();
                _busyWatch.Reset();
            }
        }

        public void EnterBusy()
        {
            if (!_wasIdle)
                return;
            _wasIdle = false;

            lock (_statisticsLock)
            {
                _totalIdleWatch.Stop();
                _idleWatch.Reset();

                _totalBusyWatch.Start();
                _busyWatch.Restart();
            }
        }

        public QueueStats GetStatistics(int currentQueueLength)
        {
            lock (_statisticsLock)
            {
                var totalTime = _totalTimeWatch.Elapsed;
                var totalIdleTime = _totalIdleWatch.Elapsed;
                var totalBusyTime = _totalBusyWatch.Elapsed;
                var totalItems = Interlocked.Read(ref _totalItems);

                var lastRunMs = totalTime - _lastTotalTime;
                var lastItems = totalItems - _lastTotalItems;
                var avgItemsPerSecond = lastRunMs.Ticks != 0 ? (int)(TimeSpan.TicksPerSecond * lastItems / lastRunMs.Ticks) : 0;
                var avgProcessingTime = lastItems != 0 ? (totalBusyTime - _lastTotalBusyTime).TotalMilliseconds / lastItems : 0;
                var idleTimePercent = Math.Min(100.0, lastRunMs.Ticks != 0 ? 100.0 * (totalIdleTime - _lastTotalIdleTime).Ticks / lastRunMs.Ticks : 100);

                var stats = new QueueStats(
                    Name,
                    GroupName,
                    currentQueueLength,
                    avgItemsPerSecond,
                    avgProcessingTime,
                    idleTimePercent,
                    _busyWatch.IsRunning ? _busyWatch.Elapsed : (TimeSpan?)null,
                    _idleWatch.IsRunning ? _idleWatch.Elapsed : (TimeSpan?)null,
                    totalItems,
                    _currentQueueLengthPeak,
                    _lifetimeQueueLengthPeak,
                    _lastProcessedMsgType,
                    _inProgressMsgType);

                if (totalTime - _lastTotalTime >= MinRefreshPeriod)
                {
                    _lastTotalTime = totalTime;
                    _lastTotalIdleTime = totalIdleTime;
                    _lastTotalBusyTime = totalBusyTime;
                    _lastTotalItems = totalItems;

                    _currentQueueLengthPeak = 0;
                }
                return stats;
            }
        }
    }
}

