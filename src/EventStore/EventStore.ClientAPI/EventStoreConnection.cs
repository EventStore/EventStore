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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Defines;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.TaskWrappers;
using EventStore.ClientAPI.Tcp;
using EventStore.ClientAPI.Transport.Tcp;
using Connection = EventStore.ClientAPI.Transport.Tcp.TcpTypedConnection;
using Ensure = EventStore.ClientAPI.Common.Utils.Ensure;

namespace EventStore.ClientAPI
{
    class WorkItem
    {
        public TcpPackage TcpPackage;
        public int Attempt;
        public long LastUpdatedTicks;

        public WorkItem(TcpPackage tcpPackage)
        {
            TcpPackage = tcpPackage;

            Attempt = 0;
            LastUpdatedTicks = DateTime.UtcNow.Ticks;
        }
    }

    public class EventStoreConnection : IDisposable
    {
        private const int TcpSentReceiveWindow = 50;

        private static readonly TimeSpan ReconnectionDelay = TimeSpan.FromSeconds(0.5);
        private static readonly TimeSpan EventTimeoutDelay = TimeSpan.FromSeconds(11);
        private static readonly TimeSpan EventTimeoutCheckPeriod = TimeSpan.FromSeconds(1);

        private readonly IPEndPoint _tcpEndPoint;

        private readonly TcpConnector _connector;
        private Connection _connection;
        
        private readonly ConcurrentQueue<TcpPackage> _sendQueue = new ConcurrentQueue<TcpPackage>();
        private readonly ConcurrentDictionary<Guid, WorkItem> _inProgress = new ConcurrentDictionary<Guid, WorkItem>();
        private long _inProgressCount;
        private DateTime _lastReconnectionTimestamp;
        private readonly Stopwatch _reconnectionStopwatch = new Stopwatch();
        private readonly Stopwatch _timeoutCheckStopwatch = new Stopwatch();
        private int _reconnectionsCount;
        private readonly object _connectionLock = new object();

        private Thread _monitoringThread;
        private volatile bool _monitoringThreadStop;

        private readonly ConcurrentDictionary<Guid, ITaskCompletionWrapper> _outstandingOperations;

        private bool _enableReconnect;

        public int OutstandingAsyncOperations
        {
            get { return _outstandingOperations.Count; }
        }

        public EventStoreConnection(IPEndPoint tcpEndPoint)
        {
            Ensure.NotNull(tcpEndPoint, "tcpEndPoint");

            _tcpEndPoint = tcpEndPoint;
            _connector = new TcpConnector(_tcpEndPoint);
            _outstandingOperations = new ConcurrentDictionary<Guid, ITaskCompletionWrapper>();

            _lastReconnectionTimestamp = DateTime.UtcNow;
            _connection = _connector.CreateTcpConnection(OnPackageReceived, ConnectionEstablished, ConnectionClosed);
            _timeoutCheckStopwatch.Start();

            _monitoringThread = new Thread(MonitoringLoop)
            {
                IsBackground = true,
                Name = string.Format("Monitoring thread")
            };
            _monitoringThread.Start();
        }

        //public void EnableAutoReconnect()
        //{
        //    _enableReconnect = true;
        //}

        //public void DisableAutoReconnect()
        //{
        //    _enableReconnect = false;
        //}

        public void Close()
        {
            _monitoringThreadStop = true;
            _connection.Close();
            foreach(var val in _outstandingOperations.Values)
            {
                val.Fail(new ConnectionClosingException());
            }
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        private void ConnectionEstablished(Connection tcpTypedConnection)
        {
            
        }

        private void ConnectionClosed(Connection tcpTypedConnection, IPEndPoint endPoint, SocketError socketError)
        {
            lock (_connectionLock)
                _reconnectionStopwatch.Restart();
        }

        public EventStream ReadEventStream(string stream, int start, int count)
        {
            var task = ReadEventStreamAsync(stream, start, count);
            task.Wait();

            //TODO GFY THiS SHOULD HAPPEN IN COMPLETiON
            return new EventStream(stream, task.Result.Events);
        }

        public Task<ReadResult> ReadEventStreamAsync(string stream, int start, int count)
        {
            var correlationId = Guid.NewGuid();

            var dto = new ClientMessages.ReadEventsFromBeginning(correlationId, stream, start, count);

            var package = new TcpPackage(TcpCommand.ReadEventsFromBeginning, correlationId, dto.Serialize());

            var taskCompletionSource = new TaskCompletionSource<ReadResult>();
            var taskWrapper = new ReadFromBeginningTaskCompletionWrapper(taskCompletionSource);
            RegisterHandler(correlationId, taskWrapper);
            EnqueueForSend(package);

            return taskCompletionSource.Task;
        }

        public void AppendToStream(string stream, int expectedVersion, IEnumerable<Event> events)
        {
            var task = AppendToStreamAsync(stream, expectedVersion, events);
            task.Wait();
        }

        public Task<WriteResult> AppendToStreamAsync(string stream, int expectedVersion, IEnumerable<Event> events)
        {
            var correlationId = Guid.NewGuid();

            var eventDtos = events.Select(x => new ClientMessages.Event(x.EventId,
                                                                          x.Type,
                                                                          x.Data,
                                                                          x.Metadata)).ToArray();

            var dto = new ClientMessages.WriteEvents(correlationId,
                                                       stream,
                                                       expectedVersion,
                                                       eventDtos);

            var package = new TcpPackage(TcpCommand.WriteEvents, correlationId, dto.Serialize());
            var taskCompletionSource = new TaskCompletionSource<WriteResult>();
            var taskWrapper = new WriteTaskCompletionWrapper(taskCompletionSource);
            RegisterHandler(correlationId, taskWrapper);
            EnqueueForSend(package);

            return taskCompletionSource.Task;
        }

        public void CreateStreamWithProtoBufMetadata(string stream, object metadata)
        {
            Ensure.NotNull(metadata, "metadata");
            var metadataBytes = metadata.Serialize();
            CreateStream(stream, metadataBytes.Array);
        }

        public Task<CreateStreamResult> CreateStreamWithProtoBufMetadataAsync(string stream, object metadata)
        {
            Ensure.NotNull(metadata, "metadata");
            var metadataBytes = metadata.Serialize();
            return CreateStreamAsync(stream, metadataBytes.Array);
        }

        public void CreateStream(string stream, byte[] metadata)
        {
            var task = CreateStreamAsync(stream, metadata);
            task.Wait();
        }

        public Task<CreateStreamResult> CreateStreamAsync(string stream, byte[] metadata)
        {
            var correlationId = Guid.NewGuid();

            var dto = new ClientMessages.CreateStream(correlationId,
                                                        stream,
                                                        metadata);

            var package = new TcpPackage(TcpCommand.CreateStream, correlationId, dto.Serialize());
            var taskCompletionSource = new TaskCompletionSource<CreateStreamResult>();
            var taskWrapper = new CreateStreamCompletionWrapper(taskCompletionSource);
            RegisterHandler(correlationId, taskWrapper);
            EnqueueForSend(package);

            return taskCompletionSource.Task;
        }

        public void DeleteStream(string stream, int expectedVersion)
        {
            var task = DeleteStreamAsync(stream, expectedVersion);
            task.Wait();
        }

        public Task<DeleteResult> DeleteStreamAsync(string stream, int expectedVersion)
        {
            var correlationId = Guid.NewGuid();
            var dto = new ClientMessages.DeleteStream(correlationId, stream, expectedVersion);

            var package = new TcpPackage(TcpCommand.DeleteStream, correlationId, dto.Serialize());
            var taskCompletionSource = new TaskCompletionSource<DeleteResult>();
            var taskWrapper = new DeleteTaskCompletionWrapper(taskCompletionSource);
            RegisterHandler(correlationId, taskWrapper);
            EnqueueForSend(package);

            return taskCompletionSource.Task;
        }

        internal void RegisterHandler(Guid correlationId, ITaskCompletionWrapper wrapper)
        {
            _outstandingOperations.TryAdd(correlationId, wrapper);
        }

        private void OnPackageReceived(Connection typedTcpConnection, TcpPackage package)
        {
            WorkItem workItem;
            ITaskCompletionWrapper wrapper;

            if (RemoveInProgressItem(package.CorrelationId, "PR", out workItem, out wrapper))
            {
            }
            else
            {
                if (!_outstandingOperations.TryGetValue(package.CorrelationId, out wrapper))
                {
                    // "SKIPPED [[ "
                    return;
                }
            }

            var result = wrapper.Process(package);
            switch (result.Status)
            {
                case ProcessResultStatus.Success:
                    wrapper.Complete();
                    break;
                case ProcessResultStatus.Retry:
                        if (wrapper.UpdateForNextAttempt())
                        {
                            RegisterHandler(wrapper.SentPackage.CorrelationId, wrapper);
                            EnqueueForSend(wrapper.SentPackage);
                        }
                        else
                            wrapper.Fail(new Exception(string.Format("Retry is not supported in wrapper {0}.", wrapper.GetType())));
                    break;
                case ProcessResultStatus.NotifyError:
                    wrapper.Fail(result.Exception);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal void EnqueueForSend(TcpPackage package)
        {
            while (_sendQueue.Count > TcpSentReceiveWindow)
                Thread.Sleep(1);
            
            _sendQueue.Enqueue(package);
        }

        private void MonitoringLoop()
        {
            while (!_monitoringThreadStop)
            {
                TcpPackage nextPackage;
                if (_inProgressCount < TcpSentReceiveWindow && _sendQueue.TryDequeue(out nextPackage))
                {
                    Interlocked.Increment(ref _inProgressCount);

                    var workItem = new WorkItem(nextPackage);

                    Send(workItem, null);
                }
                else
                    Thread.Sleep(1);

                lock (_connectionLock)
                {
                    if (_reconnectionStopwatch.IsRunning && _reconnectionStopwatch.Elapsed >= ReconnectionDelay)
                    {
                        _reconnectionsCount += 1;
                        _lastReconnectionTimestamp = DateTime.UtcNow;
                        _connection = _connector.CreateTcpConnection(OnPackageReceived, 
                                                                     ConnectionEstablished, 
                                                                     ConnectionClosed);
                        _reconnectionStopwatch.Stop();
                    }
                }

                if (_timeoutCheckStopwatch.Elapsed > EventTimeoutCheckPeriod)
                {
                    var now = DateTime.UtcNow;
                    foreach (var kvp in _inProgress)
                    {
                        var correlationId = kvp.Key;
                        var workerItem = kvp.Value;

                        var lastUpdated = new DateTime(Interlocked.Read(ref workerItem.LastUpdatedTicks));
                        if (now - lastUpdated > EventTimeoutDelay || _reconnectionsCount > 10)
                        {
                            if (lastUpdated > _lastReconnectionTimestamp || _reconnectionsCount > 10)
                            {
                                WorkItem workItem;
                                ITaskCompletionWrapper completionWrapper;
                                if (RemoveInProgressItem(correlationId, " -ML timeout", out workItem, out completionWrapper))
                                {
                                    completionWrapper.Fail(
                                        new Exception(
                                                string.Format("Timed out event {0} which "
                                                            + "never got response from server was discovered. "
                                                            + "Last state update: {1}, last reconnect: {2}, now: {3}.",
                                                            workerItem.TcpPackage.CorrelationId,
                                                            lastUpdated,
                                                            _lastReconnectionTimestamp,
                                                            now)));
                                }

                                
                            }
                            else
                                Retry(correlationId);
                        }
                    }
                    _timeoutCheckStopwatch.Restart();
                }
            }

        }

        private bool RemoveInProgressItem(Guid correlationId, string marker, 
            out WorkItem workItem, out ITaskCompletionWrapper wrapper)
        {
            lock (_connectionLock)
            {
                if (_inProgress.TryRemove(correlationId, out workItem))
                {
                    Interlocked.Decrement(ref _inProgressCount);
                    return _outstandingOperations.TryRemove(correlationId, out wrapper);
                }
                wrapper = null;
                return false;
            }
        }

        private void Send(WorkItem workItem, ITaskCompletionWrapper wrapper)
        {
            lock (_connectionLock)
            {
                var correlationId = workItem.TcpPackage.CorrelationId;
                _inProgress.TryAdd(correlationId, workItem);

                if (wrapper != null)
                    _outstandingOperations.TryAdd(correlationId, wrapper);
                else
                {
                    ITaskCompletionWrapper unused;
                    Debug.Assert(_outstandingOperations.TryGetValue(correlationId, out unused),
                        "Initially wrapper should be prsebt in outstanding tasks");
                }
                _connection.EnqueueSend(workItem.TcpPackage.AsByteArray());
            }
        }

        private void Retry(Guid correlationId)
        {
            //lock (_connectionLock)
            {
                WorkItem inProgressItem;
                ITaskCompletionWrapper wrapper;

                if (RemoveInProgressItem(correlationId, " IN Retry", out inProgressItem, out wrapper))
                {
                    var newCorrelationId = Guid.NewGuid();
                    var newPackage = new TcpPackage(inProgressItem.TcpPackage.Command, 
                                                    newCorrelationId,
                                                    inProgressItem.TcpPackage.Data);
                    inProgressItem.Attempt += 1;
                    inProgressItem.TcpPackage = newPackage;
                    Interlocked.Exchange(ref inProgressItem.LastUpdatedTicks, DateTime.UtcNow.Ticks);

                    if (inProgressItem.Attempt > 100)
                    {
                       wrapper.Fail(new Exception(string.Format("Item's {0} current attempt is {1}!",
                                                                            inProgressItem,
                                                                            inProgressItem.Attempt)));
                    }
                    else
                    {
                            wrapper.SentPackage = newPackage;
                            Send(inProgressItem, wrapper);
                    }
                }
                else
                {
                    ITaskCompletionWrapper completionWrapper;
                    if (_outstandingOperations.TryRemove(correlationId, out completionWrapper))
                    {
                        completionWrapper.Fail(new Exception(string.Format("Item {0} was not found to perform retry on ",
                                                                           correlationId)));
                    }
                }
            }
        }
    }
}