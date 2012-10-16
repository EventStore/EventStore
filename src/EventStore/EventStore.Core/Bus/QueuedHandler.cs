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
using System.Diagnostics;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Monitoring.Stats;

namespace EventStore.Core.Bus
{
    public class QueuedHandler : IHandle<Message>, IPublisher, IMonitoredQueue, IThreadSafePublisher
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<QueuedHandler>();

        public int MessageCount { get { return _queue.Count; } }
        public string Name { get { return _name; } }

        private readonly IHandle<Message> _consumer;
        private readonly string _name;
        private readonly bool _watchSlowMsg;

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
        
        private DateTime? _currentItemProcessingStarted;
        private DateTime? _idleTimeStarted;

        private readonly Stopwatch _processingTime = new Stopwatch();
        private readonly Stopwatch _idleTime = new Stopwatch();
        private readonly Stopwatch _allTime = new Stopwatch();

        private long _lastProcessingTime;
        private long _lastIdleTime;
        private long _lastAllTime;
        private long _lastProcessed;

        private long _totalItemsProcessed;
        private long _lengthLifetimePeak;
        private long _lengthCurrentTryPeak;

        private Type _lastProcessedMsgType;
        private Type _inProgressMsgType;

        private readonly Stopwatch _slowMsgWatch = new Stopwatch();
        private readonly int _slowMsgThresholdMs;

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
            _slowMsgThresholdMs = slowMsgThresholdMs ?? InMemoryBus.DefaultSlowMessageThresholdMs;
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
            _allTime.Start();
            while (!_stop)
            {
                Message msg = null;
                try
                {
                    if (_queue.TryDequeue(out msg))
                    {
                        var ttlMessage = msg as IAmOnlyCaredAboutForTime;
                        if (ttlMessage != null && !ttlMessage.AmStillCaredAbout()) 
                            continue;

                        //NOTE: the following locks are primarily acquired in this thread, 
                        //      so not too high performance penalty
                        lock (_statisticsLock)
                        {
                            _idleTime.Stop();
                            _currentItemProcessingStarted = DateTime.UtcNow;
                            _idleTimeStarted = null;

                            var cnt = _queue.Count;
                            _lengthLifetimePeak = Math.Max(_lengthLifetimePeak, cnt);
                            _lengthCurrentTryPeak = Math.Max(_lengthCurrentTryPeak, cnt);

                            _inProgressMsgType = msg.GetType();

                            _processingTime.Start();
                        }

                        if (!_watchSlowMsg)
                        {
                            _consumer.Handle(msg);
                            _totalItemsProcessed++;
                        }
                        else
                        {
                            _slowMsgWatch.Restart();
                            var qSize = _queue.Count;

                            _consumer.Handle(msg);
                            _totalItemsProcessed++;

                            if (_slowMsgWatch.ElapsedMilliseconds > _slowMsgThresholdMs)
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
                            _processingTime.Stop();
                            _lastProcessedMsgType = _inProgressMsgType;
                            _inProgressMsgType = null;
                            _currentItemProcessingStarted = null;
                            _idleTimeStarted = DateTime.UtcNow;
                            _idleTime.Start();
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    Log.ErrorException(ex, "Error while processing message {0} in queued handler '{1}'.", msg, _name);
                }
            }
            _stopped.Set();
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
            var now = DateTime.UtcNow;
            var itemsProcessedInTotal = Interlocked.Read(ref _totalItemsProcessed);
            lock (_statisticsLock)
            {
                var itemProcessedInLastRun = (itemsProcessedInTotal - _lastProcessed);
                var elapsedAllTimeInLastRun = _allTime.ElapsedMilliseconds - _lastAllTime;
                var stats = new QueueStats(
                    _name,
                    _queue.Count,
                    elapsedAllTimeInLastRun == 0 ? 0 : (int)((1000 * itemProcessedInLastRun) / elapsedAllTimeInLastRun),
                    itemProcessedInLastRun == 0  ? 0 : (float)(_processingTime.ElapsedMilliseconds - _lastProcessingTime) / itemProcessedInLastRun,
                    elapsedAllTimeInLastRun == 0 ? 0 : 100.0f * (_idleTime.ElapsedMilliseconds - _lastIdleTime) / elapsedAllTimeInLastRun,
                    now - _currentItemProcessingStarted,
                    now - _idleTimeStarted,
                    itemsProcessedInTotal,
                    _lengthCurrentTryPeak,
                    _lengthLifetimePeak,
                    _lastProcessedMsgType,
                    _inProgressMsgType);

                _lastProcessingTime = _processingTime.ElapsedMilliseconds;
                _lastIdleTime = _idleTime.ElapsedMilliseconds;
                _lastAllTime = _allTime.ElapsedMilliseconds;
                _lastProcessed = itemsProcessedInTotal;

                _lengthCurrentTryPeak = 0;
                return stats;
            }
        }
    }
}

