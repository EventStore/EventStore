// Copyright (c) 2012, Event Store Ltd
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
// Neither the name of the Event Store Ltd nor the names of its
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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands.DvuAdvanced.Workers
{
    public enum WorkerRole
    {
        Writer,
        Verifier
    }

    public class Worker
    {
        public string Name
        {
            get
            {
                return _name;
            }
        }

        private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan ReconnectionDelay = TimeSpan.FromSeconds(0.5);
        private static readonly TimeSpan EventTimeoutDelay = TimeSpan.FromSeconds(7);
        private static readonly TimeSpan EventTimeoutCheckPeriod = TimeSpan.FromSeconds(1);

        private readonly string _name;
        private readonly ICoordinator _coordinator;
        private readonly WorkerRole _role;
        private readonly int _maxConcurrentItems;
        private readonly Action<TcpTypedConnection<byte[]>, TcpPackage> _onPackageArrived;
        private readonly Func<Guid, VerificationEvent, TcpPackage> _packager; 

        private readonly Thread _thread;
        private readonly ManualResetEvent _stoppedEvent = new ManualResetEvent(true);
        private volatile bool _stop;

        private int _inProgressCount;
        private long _totalProcessedEventCount;

        private readonly ConcurrentDictionary<Guid, WorkerItem> _events = new ConcurrentDictionary<Guid, WorkerItem>();

        private TcpTypedConnection<byte[]> _connection;
        private DateTime _lastReconnectionTimestamp;
        private readonly Stopwatch _reconnectionStopwatch = new Stopwatch();
        private int _reconnectionsCount;
        private readonly object _connectionLock = new object();

        private readonly Stopwatch _timeoutCheckStopwatch = new Stopwatch();

        public Worker(string name,
                      ICoordinator coordinator,
                      WorkerRole role,
                      int maxConcurrentItems,
                      Action<TcpTypedConnection<byte[]>, TcpPackage> onPackageArrived,
                      Func<Guid, VerificationEvent, TcpPackage> packager)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNull(coordinator, "coordinator");
            Ensure.NotNull(onPackageArrived, "onPackageArrived");
            Ensure.NotNull(packager, "packager");

            _name = name;
            _coordinator = coordinator;
            _role = role;
            _maxConcurrentItems = maxConcurrentItems;
            _onPackageArrived = onPackageArrived;
            _packager = packager;

            _thread = new Thread(MainLoop)
            {
                IsBackground = true,
                Name = string.Format("Worker - {0}", name)
            };
        }

        public void Start()
        {
            _lastReconnectionTimestamp = DateTime.UtcNow;
            _connection = _coordinator.CreateConnection(_onPackageArrived, OnConnectionEstablished, OnConnectionClosed);
            _timeoutCheckStopwatch.Start();
            _stoppedEvent.Reset();
            _thread.Start();
        }

        public void Stop()
        {
            _stop = true;
            _stoppedEvent.WaitOne(StopTimeout);
        }

        private void MainLoop()
        {
            while (!_stop)
            {
                VerificationEvent evnt;
                if (_inProgressCount < _maxConcurrentItems && TryGetNext(out evnt))
                {
                    Interlocked.Increment(ref _inProgressCount);
                    Send(evnt);
                }
                else
                    Thread.Sleep(1);

                lock (_connectionLock)
                {
                    if (_reconnectionStopwatch.IsRunning && _reconnectionStopwatch.Elapsed >= ReconnectionDelay)
                    {
                        _reconnectionsCount += 1;
                        _lastReconnectionTimestamp = DateTime.UtcNow;
                        _connection = _coordinator.CreateConnection(_onPackageArrived, OnConnectionEstablished, OnConnectionClosed);
                        _reconnectionStopwatch.Stop();
                    }
                }

                if (_timeoutCheckStopwatch.Elapsed > EventTimeoutCheckPeriod)
                {
                    var now = DateTime.UtcNow;
                    foreach (var workerItem in _events.Values)
                    {
                        var lastUpdated = new DateTime(Interlocked.Read(ref workerItem.LastUpdatedTicks));
                        if (now - lastUpdated > EventTimeoutDelay)
                        {
                            if (lastUpdated > _lastReconnectionTimestamp)
                            {
                                SignalWorkerFailed(
                                    error: string.Format("Worker {0} discovered timed out event which "
                                                         + "never got response from server. "
                                                         + "Last state update: {1}, last reconnect: {2}, now: {3}.",
                                                         Name,
                                                         lastUpdated,
                                                         _lastReconnectionTimestamp,
                                                         now));
                                TryRemoveWorkItem(workerItem);
                            }
                            else
                                Retry(workerItem);
                        }
                    }
                    _timeoutCheckStopwatch.Restart();
                }
            }

            _stoppedEvent.Set();
        }

        private bool TryGetNext(out VerificationEvent evnt)
        {
            switch (_role)
            {
                case WorkerRole.Writer:
                    return _coordinator.TryGetEventToWrite(out evnt);
                case WorkerRole.Verifier:
                    return _coordinator.TryGetEventToVerify(out evnt);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public bool TryGetWorkItem(Guid key, out WorkerItem value)
        {
            return _events.TryGetValue(key, out value);
        }

        public bool TryRemoveWorkItem(WorkerItem workItem)
        {
            if (!_events.TryRemove(workItem.CorrelationId, out workItem))
            {
                SignalWorkerFailed(
                    error: string.Format("Worker {0} couldn't remove event '{1}'. Concurrency failure!",
                                         Name,
                                         workItem.Event.Event.EventId));
                return false;
            }
            Interlocked.Decrement(ref _inProgressCount);
            return true;
        }

        public void Send(VerificationEvent evnt)
        {
            lock (_connectionLock)
            {
                var corrId = Guid.NewGuid();
                _events.TryAdd(corrId, new WorkerItem(corrId, evnt));
                _connection.EnqueueSend(_packager(corrId, evnt).AsByteArray());
            }
        }

        public void Retry(WorkerItem item)
        {
            lock (_connectionLock)
            {
                if (_events.TryRemove(item.CorrelationId, out item))
                {
                    var newCorrId = Guid.NewGuid();

                    item.CorrelationId = newCorrId;
                    item.Attempt += 1;
                    Interlocked.Exchange(ref item.LastUpdatedTicks, DateTime.UtcNow.Ticks);

                    _events.TryAdd(newCorrId, item);

                    _connection.EnqueueSend(_packager(newCorrId, item.Event).AsByteArray());
                }
                else
                    SignalWorkerFailed(null,
                                       string.Format("Worker {0} couldn't remove event '{1}' on retry. Concurrency failure!",
                                                     Name,
                                                     item != null
                                                               ? item.CorrelationId.ToString()
                                                               : "null"));
            }
        }

        public void NotifyItemProcessed(WorkerItem item)
        {
            switch (_role)
            {
                case WorkerRole.Writer:
                    Interlocked.Increment(ref _totalProcessedEventCount);
                    _coordinator.NotifyEventCommitted(item.Event);
                    break;
                case WorkerRole.Verifier:
                    Interlocked.Increment(ref _totalProcessedEventCount);
                    _coordinator.NotifyEventVerified(item.Event);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void SignalWorkerFailed(Exception exception = null, string error = null)
        {
            _coordinator.SignalWorkerFailed(exception, error);
        }

        private void OnConnectionEstablished(TcpTypedConnection<byte[]> connection) { }

        private void OnConnectionClosed(TcpTypedConnection<byte[]> connection, SocketError errorCode)
        {
            lock (_connectionLock)
            {
                _reconnectionStopwatch.Restart();
            }
        }
    }
}
