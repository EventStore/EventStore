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
using System.Net.Sockets;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands.DvuAdvanced
{
#if __MonoCS__
    using ConcurrentVerificationEventQueue = Common.ConcurrentCollections.ConcurrentQueue<VerificationEvent>;
#else
    using ConcurrentVerificationEventQueue = System.Collections.Concurrent.ConcurrentQueue<VerificationEvent>;
#endif

    public interface ICoordinator
    {
        bool TryGetEventToWrite(out VerificationEvent evnt);
        bool TryGetEventToVerify(out VerificationEvent evnt);

        void NotifyEventCommitted(VerificationEvent @event);
        void NotifyEventVerified(VerificationEvent @event);

        void SignalWorkerFailed(Exception exception = null, string error = null);

        TcpTypedConnection<byte[]> CreateConnection(Action<TcpTypedConnection<byte[]>, TcpPackage> packageReceived,
                                                    Action<TcpTypedConnection<byte[]>> connectionEstablished,
                                                    Action<TcpTypedConnection<byte[]>, SocketError> connectionClosed);
    }

    public class Coordinator : ICoordinator
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<Coordinator>();
        public const int MaxQueueSize = 100000;
        public const int NotificationStep = 1000;

        private long _totalWritten;
        private long _totalVerified;

        private readonly ConcurrentVerificationEventQueue _eventsForWriting;
        private readonly ConcurrentVerificationEventQueue _eventsForVerification;

        private readonly CommandProcessorContext _context;
        private readonly IProducer[] _producers;
        private readonly int _totalEvents;

        private readonly AutoResetEvent _createdEvent;
        private readonly AutoResetEvent _doneEvent;

        public Coordinator(CommandProcessorContext context, 
                           IProducer[] producers, 
                           int totalEvents,
                           AutoResetEvent createdEvent, 
                           AutoResetEvent doneEvent)
        {
            Ensure.NotNull(context, "context");
            Ensure.NotNull(producers, "producers");
            Ensure.NotNull(createdEvent, "createdEvent");
            Ensure.NotNull(doneEvent, "doneEvent");

            _context = context;
            _producers = producers;
            _totalEvents = totalEvents;
            _createdEvent = createdEvent;
            _doneEvent = doneEvent;

            _eventsForWriting = new ConcurrentVerificationEventQueue();
            _eventsForVerification = new ConcurrentVerificationEventQueue();
        }

        public void Start()
        {
            new Thread(Create) {IsBackground = true, Name = "coordinator create thread"}.Start();
        }

        private void Create()
        {
            var remainder = _totalEvents;

            while (remainder > 0)
            {
                if (_eventsForWriting.Count < MaxQueueSize)
                {
                    var producer = _producers[remainder % _producers.Length];
                    _eventsForWriting.Enqueue(producer.Next());
                    remainder--;
                }
                else
                    Thread.Sleep(1);
            }

            _createdEvent.Set();
        }

        public bool TryGetEventToWrite(out VerificationEvent evnt)
        {
            return _eventsForWriting.TryDequeue(out evnt);
        }

        public bool TryGetEventToVerify(out VerificationEvent evnt)
        {
            return _eventsForVerification.TryDequeue(out evnt);
        }

        public void NotifyEventCommitted(VerificationEvent @event)
        {
            _eventsForVerification.Enqueue(@event);

            var com = Interlocked.Increment(ref _totalWritten);
            if (com % NotificationStep == 0)
            {
                var table = new ConsoleTable("WORKER", "COMMITTED");
                table.AppendRow("WRITER", com.ToString());
                Log.Info(table.CreateIndentedTable());
            }
        }

        public void NotifyEventVerified(VerificationEvent @event)
        {
            var ver = Interlocked.Increment(ref _totalVerified);
            if (ver % NotificationStep == 0)
            {
                var table = new ConsoleTable("WORKER", "VERIFIED");
                table.AppendRow("VERIFIER", ver.ToString());
                Log.Info(table.CreateIndentedTable());
            }

            var committed = _totalWritten;
            if (committed == _totalEvents)
                _doneEvent.Set();
        }

        public void SignalWorkerFailed(Exception exception = null, string error = null)
        {
            if (exception != null)
                Log.FatalException(exception, "Error : {0}", error);
            else
                Log.Fatal("Error : {0}", error);
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
