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
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands.DvuAdvanced
{
#if __MonoCS__
    using ConcurrentTasksQueue = Common.ConcurrentCollections.ConcurrentQueue<Task>;
#else
    using ConcurrentTasksQueue = System.Collections.Concurrent.ConcurrentQueue<Task>;
#endif

    public interface ICoordinator
    {
        bool TryGetTask(out Task task);
        void Complete(Task task);
        bool IsDeleted(string eventStreamId);
        void SignalWorkerFailed(Exception exception = null, string error = null);
        void SignalPossibleFailure(string error);

        TcpTypedConnection<byte[]> CreateConnection(Action<TcpTypedConnection<byte[]>, TcpPackage> packageReceived,
                                                    Action<TcpTypedConnection<byte[]>> connectionEstablished,
                                                    Action<TcpTypedConnection<byte[]>, SocketError> connectionClosed);
    }

    public class Coordinator : ICoordinator
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<Coordinator>();

        public const int MaxQueueSize = 10000;
        public const int NotificationStep = 1000;

        private long _completed;
        private readonly ConcurrentTasksQueue _tasks = new ConcurrentTasksQueue();

        private readonly List<string> _deletedStreams = new List<string>();
        private readonly object _deletedStreamsLock = new object();

        private readonly CommandProcessorContext _context;
        private readonly IProducer[] _producers;
        private readonly int _totalTasks;
        private readonly AutoResetEvent _doneEvent;

        public Coordinator(CommandProcessorContext context, 
                           IProducer[] producers, 
                           int totalTasks,
                           AutoResetEvent doneEvent)
        {
            Ensure.NotNull(context, "context");
            Ensure.NotNull(producers, "producers");
            Ensure.NotNull(doneEvent, "doneEvent");

            _context = context;
            _producers = producers;
            _totalTasks = totalTasks;
            _doneEvent = doneEvent;
        }

        public void Start()
        {
            new Thread(Create) {IsBackground = true, Name = "coordinator's create thread"}.Start();
        }

        private void Create()
        {
            var remainder = _totalTasks;

            while (remainder > 0)
            {
                if (_tasks.Count < MaxQueueSize)
                {
                    var producer = _producers[remainder % _producers.Length];
                    _tasks.Enqueue(producer.Next());
                    remainder--;
                }
                else
                    Thread.Sleep(1);
            }
        }

        public bool TryGetTask(out Task task)
        {
            return _tasks.TryDequeue(out task);
        }

        public void Complete(Task task)
        {
            if (task.MoveToNextStep())
            {
                _tasks.Enqueue(task);
            }
            else
            {
                if (task.Type == TaskType.DeleteStream)
                    MarkStreamAsDeleted(((DeleteStreamTask)task).EventStreamId);

                var completed = Interlocked.Increment(ref _completed);
                if (completed % NotificationStep == 0)
                    Log.Info("Tasks done : {0}", completed);

                if (completed == _totalTasks)
                    _doneEvent.Set();
            }
        }

        private void MarkStreamAsDeleted(string eventStreamId)
        {
            lock (_deletedStreamsLock)
                _deletedStreams.Add(eventStreamId);
        }

        public bool IsDeleted(string eventStreamId)
        {
            lock (_deletedStreamsLock)
                return _deletedStreams.Contains(eventStreamId);
        }

        public void SignalWorkerFailed(Exception exception = null, string error = null)
        {
            if (exception != null)
                Log.FatalException(exception, "Error : {0}", error);
            else
                Log.Fatal("Error : {0}", error);
        }

        public void SignalPossibleFailure(string error)
        {
            Log.Error("Warning : {0}", error);
        }

        public TcpTypedConnection<byte[]> CreateConnection(Action<TcpTypedConnection<byte[]>, TcpPackage> packageReceived,
                                                           Action<TcpTypedConnection<byte[]>> connectionEstablished,
                                                           Action<TcpTypedConnection<byte[]>, SocketError> connectionClosed)
        {
            return _context.Client.CreateTcpConnection(_context,
                                                       packageReceived,
                                                       connectionEstablished,
                                                       connectionClosed,
                                                       false);
        }
    }
}
