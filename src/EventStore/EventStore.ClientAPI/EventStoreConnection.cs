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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Defines;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.TaskWrappers;
using EventStore.ClientAPI.Transport.Tcp;
using Connection = EventStore.ClientAPI.Transport.Tcp.TcpTypedConnection;
using Ensure = EventStore.ClientAPI.Common.Utils.Ensure;

namespace EventStore.ClientAPI
{
    internal class WorkItem
    {
        public ITaskCompletionWrapper Wrapper;

        public int Attempt;
        public long LastUpdatedTicks;

        public WorkItem(ITaskCompletionWrapper wrapper)
        {
            Ensure.NotNull(wrapper, "wrapper");
            Wrapper = wrapper;

            Attempt = 0;
            LastUpdatedTicks = DateTime.UtcNow.Ticks;
        }
    }

    public class EventStoreConnection : IDisposable
    {
        private const int MaxConcurrentItems = 50;
        private const int MaxQueueSize = 1000;

        private const int MaxAttempts = 100;

        private static readonly TimeSpan ReconnectionDelay = TimeSpan.FromSeconds(0.5);
        private static readonly TimeSpan EventTimeoutDelay = TimeSpan.FromSeconds(7);
        private static readonly TimeSpan EventTimeoutCheckPeriod = TimeSpan.FromSeconds(1);

        private readonly IPEndPoint _tcpEndPoint;

        private readonly TcpConnector _connector;
        private Connection _connection;
        private readonly object _connectionLock = new object();
        
        private readonly ConcurrentQueue<ITaskCompletionWrapper> _queue = new ConcurrentQueue<ITaskCompletionWrapper>();
        private readonly ConcurrentDictionary<Guid, WorkItem> _inProgress = new ConcurrentDictionary<Guid, WorkItem>();
        private int _inProgressCount;

        private DateTime _lastReconnectionTimestamp;
        private readonly Stopwatch _reconnectionStopwatch = new Stopwatch();
        private readonly Stopwatch _timeoutCheckStopwatch = new Stopwatch();
        private int _reconnectionsCount;

        private readonly Thread _worker;
        private volatile bool _stopping;

        public int OutstandingAsyncOperations
        {
            get
            {
                return _inProgressCount;
            }
        }

        public EventStoreConnection(IPEndPoint tcpEndPoint)
        {
            Ensure.NotNull(tcpEndPoint, "tcpEndPoint");

            _tcpEndPoint = tcpEndPoint;
            _connector = new TcpConnector(_tcpEndPoint);

            _lastReconnectionTimestamp = DateTime.UtcNow;
            _connection = _connector.CreateTcpConnection(OnPackageReceived, OnConnectionEstablished, OnConnectionClosed);
            _timeoutCheckStopwatch.Start();

            _worker = new Thread(MainLoop)
            {
                IsBackground = true,
                Name = string.Format("Worker thread")
            };
            _worker.Start();
        }

        public void Close()
        {
            _stopping = true;
            _connection.Close();

            const string err = "Work item was still in progress at the moment of manual connection closing";
            foreach(var workItem in _inProgress.Values)
                workItem.Wrapper.Fail(new ConnectionClosingException(err));
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        public void CreateStream(string stream, byte[] metadata)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var task = CreateStreamAsync(stream, metadata);
            task.Wait();
        }

        public Task<CreateStreamResult> CreateStreamAsync(string stream, byte[] metadata)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var taskCompletionSource = new TaskCompletionSource<CreateStreamResult>();
            var taskWrapper = new CreateStreamCompletionWrapper(taskCompletionSource, Guid.NewGuid(), stream, metadata);

            EnqueueOperation(taskWrapper);
            return taskCompletionSource.Task;
        }

        public void CreateStreamWithProtoBufMetadata(string stream, object metadata)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(metadata, "metadata");

            CreateStream(stream, metadata.Serialize().Array);
        }

        public Task<CreateStreamResult> CreateStreamWithProtoBufMetadataAsync(string stream, object metadata)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(metadata, "metadata");

            return CreateStreamAsync(stream, metadata.Serialize().Array);
        }

        public void DeleteStream(string stream, int expectedVersion)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var task = DeleteStreamAsync(stream, expectedVersion);
            task.Wait();
        }

        public Task<DeleteResult> DeleteStreamAsync(string stream, int expectedVersion)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var taskCompletionSource = new TaskCompletionSource<DeleteResult>();
            var taskWrapper = new DeleteTaskCompletionWrapper(taskCompletionSource, Guid.NewGuid(), stream, expectedVersion);

            EnqueueOperation(taskWrapper);
            return taskCompletionSource.Task;
        }

        public void AppendToStream(string stream, int expectedVersion, IEnumerable<Event> events)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(events, "events");

            var task = AppendToStreamAsync(stream, expectedVersion, events);
            task.Wait();
        }

        public Task<WriteResult> AppendToStreamAsync(string stream, int expectedVersion, IEnumerable<Event> events)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(events, "events");

            var taskCompletionSource = new TaskCompletionSource<WriteResult>();
            var taskWrapper = new WriteTaskCompletionWrapper(taskCompletionSource, 
                                                             Guid.NewGuid(), 
                                                             stream,
                                                             expectedVersion,
                                                             events);

            EnqueueOperation(taskWrapper);
            return taskCompletionSource.Task;
        }

        public EventStream ReadEventStream(string stream, int start, int count)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var task = ReadEventStreamAsync(stream, start, count);
            task.Wait();

            //TODO GFY THiS SHOULD HAPPEN IN COMPLETiON
            return new EventStream(stream, task.Result.Events);
        }

        public Task<ReadResult> ReadEventStreamAsync(string stream, int start, int count)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var taskCompletionSource = new TaskCompletionSource<ReadResult>();
            var taskWrapper = new ReadFromBeginningTaskCompletionWrapper(taskCompletionSource, 
                                                                         Guid.NewGuid(), 
                                                                         stream, 
                                                                         start, 
                                                                         count);

            EnqueueOperation(taskWrapper);
            return taskCompletionSource.Task;
        }

        private void EnqueueOperation(ITaskCompletionWrapper wrapper)
        {
            while (_queue.Count >= MaxQueueSize)
                Thread.Sleep(1);
            
            _queue.Enqueue(wrapper);
        }

        private void MainLoop()
        {
            while (!_stopping)
            {
                ITaskCompletionWrapper wrapper;
                if (_inProgressCount < MaxConcurrentItems && _queue.TryDequeue(out wrapper))
                {
                    Interlocked.Increment(ref _inProgressCount);
                    Send(new WorkItem(wrapper));
                }
                else
                    Thread.Sleep(1);

                lock (_connectionLock)
                {
                    if (_reconnectionStopwatch.IsRunning && _reconnectionStopwatch.Elapsed >= ReconnectionDelay)
                    {
                        _reconnectionsCount += 1;
                        _lastReconnectionTimestamp = DateTime.UtcNow;
                        _connection = _connector.CreateTcpConnection(OnPackageReceived, OnConnectionEstablished, OnConnectionClosed);
                        _reconnectionStopwatch.Stop();
                    }
                }

                if (_timeoutCheckStopwatch.Elapsed > EventTimeoutCheckPeriod)
                {
                    var now = DateTime.UtcNow;
                    foreach (var workerItem in _inProgress.Values)
                    {
                        var lastUpdated = new DateTime(Interlocked.Read(ref workerItem.LastUpdatedTicks));
                        if (now - lastUpdated > EventTimeoutDelay)
                        {
                            if (lastUpdated > _lastReconnectionTimestamp)
                            {
                                var err = string.Format("Timed out event which never got response from server was discovered. "+ 
                                                        "Last state update : {0}, last reconnect : {1}, now(utc) : {2}.",
                                                        lastUpdated,
                                                        _lastReconnectionTimestamp,
                                                        now);
                                workerItem.Wrapper.Fail(new OperationTimedOutException(err));
                                TryRemoveWorkItem(workerItem);
                            }
                            else
                                Retry(workerItem);
                        }
                    }
                    _timeoutCheckStopwatch.Restart();
                }
            }
        }

        private bool TryRemoveWorkItem(WorkItem workItem)
        {
            WorkItem removed;
            if (!_inProgress.TryRemove(workItem.Wrapper.CorrelationId, out removed))
                return false;

            Interlocked.Decrement(ref _inProgressCount);
            return true;
        }

        private void Send(WorkItem workItem)
        {
            lock (_connectionLock)
            {
                _inProgress.TryAdd(workItem.Wrapper.CorrelationId, workItem);
                _connection.EnqueueSend(workItem.Wrapper.CreateNetworkPackage().AsByteArray());
            }
        }

        private void Retry(WorkItem workItem)
        {
            lock (_connectionLock)
            {
                WorkItem inProgressItem;
                if (_inProgress.TryRemove(workItem.Wrapper.CorrelationId, out inProgressItem))
                {
                    inProgressItem.Wrapper.SetRetryId(Guid.NewGuid());
                    inProgressItem.Attempt += 1;
                    Interlocked.Exchange(ref inProgressItem.LastUpdatedTicks, DateTime.UtcNow.Ticks);

                    if (inProgressItem.Attempt > MaxAttempts)
                        inProgressItem.Wrapper.Fail(new RetriesLimitReachedException(inProgressItem.Wrapper.ToString(),
                                                                                     inProgressItem.Attempt));
                    else
                        Send(inProgressItem);
                }
                else
                    Debug.WriteLine("Concurrency failure. Unable to remove in progress item on retry");
            }
        }

        private void OnPackageReceived(Connection connection, TcpPackage package)
        {
            var corrId = package.CorrelationId;
            WorkItem workItem;

            if (!_inProgress.TryGetValue(corrId, out workItem))
            {
                Debug.WriteLine("Unexpected corrid received {0}", corrId);
                return;
            }

            var result = workItem.Wrapper.Process(package);
            switch (result.Status)
            {
                case ProcessResultStatus.Success:
                    if (TryRemoveWorkItem(workItem))
                        workItem.Wrapper.Complete();
                    break;
                case ProcessResultStatus.Retry:
                    Retry(workItem);
                    break;
                case ProcessResultStatus.NotifyError:
                    workItem.Wrapper.Fail(result.Exception);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnConnectionEstablished(Connection tcpTypedConnection) { }

        private void OnConnectionClosed(Connection connection, IPEndPoint endPoint, SocketError error)
        {
            lock (_connectionLock)
                _reconnectionStopwatch.Restart();
        }
    }
}