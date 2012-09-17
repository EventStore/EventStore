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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands.DvuAdvanced.Workers
{
    public class Worker
    {
        private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan ReconnectionDelay = TimeSpan.FromSeconds(0.5);
        private static readonly TimeSpan EventTimeoutDelay = TimeSpan.FromSeconds(7);
        private static readonly TimeSpan EventTimeoutCheckPeriod = TimeSpan.FromSeconds(1);

        private readonly string _name;
        private readonly ICoordinator _coordinator;
        private readonly int _maxConcurrentItems;

        private readonly Thread _thread;

        private readonly ManualResetEvent _stoppedEvent = new ManualResetEvent(true);
        private volatile bool _stop;

        private int _inProgressCount;
        private long _totalProcessedEventCount;

        private readonly ConcurrentDictionary<Guid, WorkerItem> _items = new ConcurrentDictionary<Guid, WorkerItem>();

        private TcpTypedConnection<byte[]> _connection;
        private DateTime _lastReconnectionTimestamp;
        private readonly Stopwatch _reconnectionStopwatch = new Stopwatch();
        private int _reconnectionsCount;
        private readonly object _connectionLock = new object();

        private readonly Stopwatch _timeoutCheckStopwatch = new Stopwatch();

        public Worker(string name, ICoordinator coordinator, int maxConcurrentItems)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNull(coordinator, "coordinator");

            _name = name;
            _coordinator = coordinator;
            _maxConcurrentItems = maxConcurrentItems;

            _thread = new Thread(MainLoop)
            {
                IsBackground = true,
                Name = string.Format("Worker {0} main loop", name)
            };
        }

        public void Start()
        {
            _lastReconnectionTimestamp = DateTime.UtcNow;
            _connection = _coordinator.CreateConnection(OnPackageArrived, OnConnectionEstablished, OnConnectionClosed);
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
                Task task;
                if (_inProgressCount < _maxConcurrentItems && _coordinator.TryGetTask(out task))
                {
                    Interlocked.Increment(ref _inProgressCount);
                    Send(task);
                }
                else
                    Thread.Sleep(1);

                lock (_connectionLock)
                {
                    if (_reconnectionStopwatch.IsRunning && _reconnectionStopwatch.Elapsed >= ReconnectionDelay)
                    {
                        _reconnectionsCount += 1;
                        _lastReconnectionTimestamp = DateTime.UtcNow;
                        _connection = _coordinator.CreateConnection(OnPackageArrived, OnConnectionEstablished, OnConnectionClosed);
                        _reconnectionStopwatch.Stop();
                    }
                }

                if (_timeoutCheckStopwatch.Elapsed > EventTimeoutCheckPeriod)
                {
                    var now = DateTime.UtcNow;
                    foreach (var workerItem in _items.Values)
                    {
                        var lastUpdated = new DateTime(Interlocked.Read(ref workerItem.LastUpdatedTicks));
                        if (now - lastUpdated > EventTimeoutDelay)
                        {
                            if (lastUpdated > _lastReconnectionTimestamp)
                            {
                                _coordinator.SignalWorkerFailed(null, string.Format("Worker {0} discovered timed out event which "
                                                                                    + "never got response from server. "
                                                                                    + "Last state update: {1}, last reconnect: {2}, now: {3}.",
                                                                                    _name,
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

        private bool TryRemoveWorkItem(WorkerItem workItem)
        {
            if (!_items.TryRemove(workItem.CorrelationId, out workItem))
            {
                _coordinator.SignalWorkerFailed(null, string.Format("Worker {0} couldn't remove corrid '{1}'. Concurrency failure!",
                                                                    _name,
                                                                    workItem.CorrelationId));
                return false;
            }

            Interlocked.Decrement(ref _inProgressCount);
            return true;
        }

        private void Send(Task task)
        {
            lock (_connectionLock)
            {
                var corrId = Guid.NewGuid();
                _items.TryAdd(corrId, new WorkerItem(corrId, task));
                _connection.EnqueueSend(task.CreateNetworkPackage(corrId).AsByteArray());
            }
        }

        private void Retry(WorkerItem item)
        {
            lock (_connectionLock)
            {
                if (_items.TryRemove(item.CorrelationId, out item))
                {
                    var newCorrId = Guid.NewGuid();

                    item.CorrelationId = newCorrId;
                    item.Attempt += 1;
                    Interlocked.Exchange(ref item.LastUpdatedTicks, DateTime.UtcNow.Ticks);

                    if (item.Attempt > 100)
                        _coordinator.SignalWorkerFailed(error: string.Format("Item's {0} current attempt is {1}!", 
                                                                             item, 
                                                                             item.Attempt));

                    _items.TryAdd(newCorrId, item);
                    _connection.EnqueueSend(item.Task.CreateNetworkPackage(newCorrId).AsByteArray());
                }
                else
                    _coordinator.SignalWorkerFailed(null, string.Format("Worker {0} couldn't remove corrid '{1}' on retry. Concurrency failure!",
                                                                        _name,
                                                                        item != null
                                                                            ? item.CorrelationId.ToString()
                                                                            : "null"));
            }
        }

        private void NotifyItemProcessed(WorkerItem item)
        {
            Interlocked.Increment(ref _totalProcessedEventCount);
            _coordinator.Complete(item.Task);
        }

        private void OnPackageArrived(TcpTypedConnection<byte[]> connection, TcpPackage package)
        {
            var corrId = package.CorrelationId;

            WorkerItem workItem;
            if (!_items.TryGetValue(corrId, out workItem))
            {
                _coordinator.SignalWorkerFailed(null, 
                                                string.Format(
                                                "Worker {0} received unexpected CorrId: {1}, no item with such CorrId is in progress.",
                                                _name, corrId));
                return;
            }

            var result = workItem.Task.CheckStepExpectations(package);

            switch (result.Status)
            {
                case Status.MeetsExpectations:
                    if (TryRemoveWorkItem(workItem))
                        NotifyItemProcessed(workItem);
                    break;
                case Status.Retry:
                    Retry(workItem);
                    break;
                case Status.CheckStreamDeleted:
                    if (_coordinator.IsDeleted(result.Description) && TryRemoveWorkItem(workItem))
                        NotifyItemProcessed(workItem);
                    else
                    {
                        Retry(workItem);
                        _coordinator.SignalPossibleFailure(
                            string.Format(
                                "Got stream deleted ({0}), but it's not confirmed. Retrying ({1})", result.Description,
                                workItem.Attempt));
                    }
                    break;
                case Status.Ignore:
                    break;
                case Status.FailFast:
                    _coordinator.SignalWorkerFailed(null, result.Description);
                    TryRemoveWorkItem(workItem);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
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
