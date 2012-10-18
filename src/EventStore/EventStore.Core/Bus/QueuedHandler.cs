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
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Monitoring.Stats;

namespace EventStore.Core.Bus
{
    /// <summary>
    /// Lightweight in-memory queue with a separate thread in which it passes messages
    /// to the consumer. It also tracks statistics about the message processing to help
    /// in identifying bottlenecks
    /// </summary>
    public class QueuedHandler : IHandle<Message>, IPublisher, IMonitoredQueue, IThreadSafePublisher
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<QueuedHandler>();

        public int MessageCount { get { return _queue.Count; } }
        public string Name { get { return _name; } }

        private readonly IHandle<Message> _consumer;
        private readonly string _name;

        private readonly bool _watchSlowMsg;
        private readonly Stopwatch _slowMsgWatch = new Stopwatch();
        private readonly TimeSpan _slowMsgThreshold;

#if __MonoCS__
        private readonly Common.ConcurrentCollections.ConcurrentQueue<Message> _queue = new Common.ConcurrentCollections.ConcurrentQueue<Message>();
#else
        private readonly System.Collections.Concurrent.ConcurrentQueue<Message> _queue = new System.Collections.Concurrent.ConcurrentQueue<Message>();
#endif

        private Thread _thread;
        private volatile bool _stop;
        private readonly ManualResetEvent _stopped = new ManualResetEvent(false);
        private readonly int _threadStopWaitTimeoutMs;

        // monitoring
        private readonly QueueMonitor _queueMonitor;
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
        private long _totalSkipped;
        private long _lastTotalSkipped;
        private long _lifetimeQueueLengthPeak;
        private long _currentQueueLengthPeak;
        private Type _lastProcessedMsgType;
        private Type _inProgressMsgType;

        public QueuedHandler(IHandle<Message> consumer,
                             string name,
                             bool watchSlowMsg = true,
                             int? slowMsgThresholdMs = null,
                             int threadStopWaitTimeoutMs = 10000)
        {
            Ensure.NotNull(consumer, "consumer");
            Ensure.NotNull(name, "name");

            _consumer = consumer;
            _name = name;
            _watchSlowMsg = watchSlowMsg;
            _slowMsgThreshold = TimeSpan.FromMilliseconds(slowMsgThresholdMs ?? InMemoryBus.DefaultSlowMessageThresholdMs);
            _threadStopWaitTimeoutMs = threadStopWaitTimeoutMs;

            _queueMonitor = QueueMonitor.Default;
        }

        public void Start()
        {
            if (_thread != null)
                throw new InvalidOperationException("Already a thread running.");

            _queueMonitor.Register(this);

            _thread = new Thread(ReadFromQueue);
            _thread.IsBackground = true;
            _thread.Name = _name;
            _thread.Start();
        }

        public void Stop()
        {
            _stop = true;
            if (!_stopped.WaitOne(_threadStopWaitTimeoutMs))
                throw new TimeoutException(string.Format("Unable to stop thread '{0}'.", _name));
            _queueMonitor.Unregister(this);
        }

        private void ReadFromQueue(object o)
        {
            Thread.BeginThreadAffinity(); // ensure we are not switching between OS threads. Required at least for v8.
            _totalTimeWatch.Start();
            while (!_stop)
            {
                Message msg = null;
                try
                {
                    if (!_queue.TryDequeue(out msg))
                    {
                        Thread.Sleep(1);
                    }
                    else
                    {
                        var ttlMessage = msg as IAmOnlyCaredAboutForTime;
                        if (ttlMessage != null && !ttlMessage.AmStillCaredAbout())
                        {
                            _totalSkipped += 1;
                            continue;
                        }

                        //NOTE: the following locks are primarily acquired in this thread, 
                        //      so not too high performance penalty
                        lock (_statisticsLock)
                        {
                            _totalIdleWatch.Stop();
                            _idleWatch.Reset();
                           
                            _totalBusyWatch.Start();
                            _busyWatch.Restart();

                            var cnt = _queue.Count;
                            _lifetimeQueueLengthPeak = _lifetimeQueueLengthPeak > cnt ? _lifetimeQueueLengthPeak : cnt;
                            _currentQueueLengthPeak = _currentQueueLengthPeak > cnt ? _currentQueueLengthPeak : cnt;

                            _inProgressMsgType = msg.GetType();
                        }

                        if (!_watchSlowMsg)
                        {
                            _consumer.Handle(msg);
                            _totalItems += 1;
                        }
                        else
                        {
                            _slowMsgWatch.Restart();
                            var qSize = _queue.Count;

                            _consumer.Handle(msg);
                            _totalItems += 1;

                            if (_slowMsgWatch.Elapsed > _slowMsgThreshold)
                            {
                                Log.Trace("SLOW QUEUE MSG [{0}]: {1} - {2}ms. Q: {3}/{4}.",
                                          _name,
                                          msg.GetType().Name,
                                          _slowMsgWatch.ElapsedMilliseconds,
                                          qSize,
                                          _queue.Count);
                            }
                        }

                        lock (_statisticsLock)
                        {
                            _lastProcessedMsgType = _inProgressMsgType;
                            _inProgressMsgType = null;
                            _totalIdleWatch.Start();
                            _idleWatch.Restart();
                            _totalBusyWatch.Stop();
                            _busyWatch.Reset();
                        }
                    }
                    }
                catch (Exception ex)
                {
                    Log.ErrorException(ex, "Error while processing message {0} in queued handler '{1}'.", msg, _name);
                }
            }
            _stopped.Set();
            Thread.EndThreadAffinity();
        }

        public void Publish(Message message)
        {
            Ensure.NotNull(message, "message");
            _queue.Enqueue(message);
        }

        public void Handle(Message message)
        {
            Ensure.NotNull(message, "message");
            _queue.Enqueue(message);
        }

        public QueueStats GetStatistics()
        {
            lock (_statisticsLock)
            {
                var totalTime = _totalTimeWatch.Elapsed;
                var totalIdleTime = _totalIdleWatch.Elapsed;
                var totalBusyTime = _totalBusyWatch.Elapsed;
                var totalItems = Interlocked.Read(ref _totalItems);
                var totalSkipped = Interlocked.Read(ref _totalSkipped);

                var lastRunMs = (long)(totalTime - _lastTotalTime).TotalMilliseconds;
                var lastItems = totalItems - _lastTotalItems;
                var avgItemsPerSecond = lastRunMs != 0 ? (int)(1000 * lastItems / lastRunMs) : 0;
                var avgProcessingTime = lastItems != 0 ? (totalBusyTime - _lastTotalBusyTime).TotalMilliseconds / lastItems : 0;
                var idleTimePercent = lastRunMs != 0 ? 100.0 * (totalIdleTime - _lastTotalIdleTime).TotalMilliseconds / lastRunMs : 0;

                var stats = new QueueStats(
                    _name,
                    _queue.Count,
                    avgItemsPerSecond,
                    avgProcessingTime,
                    idleTimePercent,
                    _busyWatch.IsRunning ? _busyWatch.Elapsed : (TimeSpan?)null,
                    _idleWatch.IsRunning ? _idleWatch.Elapsed : (TimeSpan?)null,
                    totalItems,
                    _currentQueueLengthPeak,
                    _lifetimeQueueLengthPeak,
                    _lastProcessedMsgType,
                    _inProgressMsgType,
                    totalSkipped,
                    _totalSkipped - _lastTotalSkipped);

                _lastTotalTime = totalTime;
                _lastTotalIdleTime = totalIdleTime;
                _lastTotalBusyTime = totalBusyTime;
                _lastTotalItems = totalItems;
                _lastTotalSkipped = totalSkipped;

                _currentQueueLengthPeak = 0;
                return stats;
            }
        }
    }
}

