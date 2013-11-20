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
    public class QueuedHandlerAutoReset : IQueuedHandler, IHandle<Message>, IPublisher, IMonitoredQueue, IThreadSafePublisher
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<QueuedHandlerAutoReset>();

        public int MessageCount { get { return _queue.Count; } }
        public string Name { get { return _queueStats.Name; } }

        private readonly IHandle<Message> _consumer;

        private readonly bool _watchSlowMsg;
        private readonly TimeSpan _slowMsgThreshold;

        private readonly Common.Concurrent.ConcurrentQueue<Message> _queue = new Common.Concurrent.ConcurrentQueue<Message>();
        private readonly AutoResetEvent _msgAddEvent = new AutoResetEvent(false);

        private Thread _thread;
        private volatile bool _stop;
        private volatile bool _starving;
        private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(true);
        private readonly TimeSpan _threadStopWaitTimeout;

        // monitoring
        private readonly QueueMonitor _queueMonitor;
        private readonly QueueStatsCollector _queueStats;

        public QueuedHandlerAutoReset(IHandle<Message> consumer,
                                      string name,
                                      bool watchSlowMsg = true,
                                      TimeSpan? slowMsgThreshold = null,
                                      TimeSpan? threadStopWaitTimeout = null,
                                      string groupName = null)
        {
            Ensure.NotNull(consumer, "consumer");
            Ensure.NotNull(name, "name");

            _consumer = consumer;

            _watchSlowMsg = watchSlowMsg;
            _slowMsgThreshold = slowMsgThreshold ?? InMemoryBus.DefaultSlowMessageThreshold;
            _threadStopWaitTimeout = threadStopWaitTimeout ?? QueuedHandler.DefaultStopWaitTimeout;

            _queueMonitor = QueueMonitor.Default;
            _queueStats = new QueueStatsCollector(name, groupName);
        }

        public void Start()
        {
            if (_thread != null)
                throw new InvalidOperationException("Already a thread running.");

            _queueMonitor.Register(this);

            _stopped.Reset();

            _thread = new Thread(ReadFromQueue) {IsBackground = true, Name = Name};
            _thread.Start();
        }

        public void Stop()
        {
            _stop = true;
            if (!_stopped.Wait(_threadStopWaitTimeout))
                throw new TimeoutException(string.Format("Unable to stop thread '{0}'.", Name));
        }

        public void RequestStop()
        {
            _stop = true;
        }

        private void ReadFromQueue(object o)
        {
            _queueStats.Start();
            Thread.BeginThreadAffinity(); // ensure we are not switching between OS threads. Required at least for v8.

            const int spinmax = 5000;
            const int sleepmax = 500;
            var iterationsCount = 0;
            while (!_stop)
            {
                Message msg = null;
                try
                {
                    if (!_queue.TryDequeue(out msg))
                    {
                        _queueStats.EnterIdle();

                        iterationsCount += 1;
                        if (iterationsCount < spinmax)
                        {
                            //do nothing... spin
                        } 
                        else if (iterationsCount < sleepmax)
                        {
                            Thread.Sleep(1);
                        } 
                        else
                        {
                            _starving = true;
                            _msgAddEvent.WaitOne(100);
                            _starving = false;
                        }
                    }
                    else
                    {
                        _queueStats.EnterBusy();

                        iterationsCount = 0;

                        var cnt = _queue.Count;
                        _queueStats.ProcessingStarted(msg.GetType(), cnt);

                        if (_watchSlowMsg)
                        {
                            var start = DateTime.UtcNow;

                            _consumer.Handle(msg);

                            var elapsed = DateTime.UtcNow - start;
                            if (elapsed > _slowMsgThreshold)
                            {
                                Log.Trace("SLOW QUEUE MSG [{0}]: {1} - {2}ms. Q: {3}/{4}.",
                                          Name, _queueStats.InProgressMessage.Name, (int)elapsed.TotalMilliseconds, cnt, _queue.Count);
                                if (elapsed > QueuedHandler.VerySlowMsgThreshold && !(msg is SystemMessage.SystemInit))
                                    Log.Error("---!!! VERY SLOW QUEUE MSG [{0}]: {1} - {2}ms. Q: {3}/{4}.",
                                              Name, _queueStats.InProgressMessage.Name, (int)elapsed.TotalMilliseconds, cnt, _queue.Count);
                            }
                        }
                        else
                        {
                            _consumer.Handle(msg);
                        }

                        _queueStats.ProcessingEnded(1);
                    }
                }
                catch (Exception ex)
                {
                    Log.ErrorException(ex, "Error while processing message {0} in queued handler '{1}'.", msg, Name);
                }
            }
            _queueStats.Stop();

            _stopped.Set();
            _queueMonitor.Unregister(this);
            Thread.EndThreadAffinity();
        }

        public void Publish(Message message)
        {
            //Ensure.NotNull(message, "message");
            _queue.Enqueue(message);
            if (_starving)
                _msgAddEvent.Set();
        }

        public void Handle(Message message)
        {
            Publish(message);
        }

        public QueueStats GetStatistics()
        {
            return _queueStats.GetStatistics(_queue.Count);
        }
    }
}

