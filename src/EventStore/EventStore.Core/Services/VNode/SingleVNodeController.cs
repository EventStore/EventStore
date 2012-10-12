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
using System.Net;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;

namespace EventStore.Core.Services.VNode
{
    public class SingleVNodeController : IHandle<Message>
    {
        public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

        private static readonly ILogger Log = LogManager.GetLoggerFor<SingleVNodeController>();

        private readonly IPublisher _outputBus;
        private readonly IPEndPoint _httpEndPoint;

        private VNodeState _state = VNodeState.Initializing;
        private QueuedHandler _mainQueue;
        private readonly VNodeFSM _fsm;

        private bool _storageReaderInitialized;
        private bool _storageWriterInitialized;
        private int _serviceShutdownsToExpect = 4;

        public SingleVNodeController(IPublisher outputBus, IPEndPoint httpEndPoint)
        {
            Ensure.NotNull(outputBus, "outputBus");
            Ensure.NotNull(httpEndPoint, "httpEndPoint");

            _outputBus = outputBus;
            _httpEndPoint = httpEndPoint;

            _fsm = CreateFSM();
        }

        public void SetMainQueue(QueuedHandler mainQueue)
        {
            Ensure.NotNull(mainQueue, "mainQueue");

            _mainQueue = mainQueue;
        }

        private VNodeFSM CreateFSM()
        {
            var stm = new VNodeFSMBuilder(() => _state)
                .InAnyState()
                    .When<SystemMessage.SystemInit>().Do(Handle)
                    .When<SystemMessage.SystemStart>().Do(Handle)
                    .When<SystemMessage.BecomeShuttingDown>().Do(Handle)
                    .When<SystemMessage.BecomeWorking>().Do(Handle)
                    .When<SystemMessage.BecomeShutdown>().Do(Handle)
                .InState(VNodeState.Initializing)
                    .When<SystemMessage.StorageReaderInitializationDone>().Do(Handle)
                    .When<SystemMessage.StorageWriterInitializationDone>().Do(Handle)
                    .When<ClientMessage.WriteMessage>().Ignore()
                    .When<ClientMessage.ReadMessage>().Ignore()
                    .WhenOther().Do(m => _outputBus.Publish(m))
                .InState(VNodeState.Master)
                    .When<ClientMessage.CreateStream>().Do(Handle)
                    .When<ClientMessage.WriteEvents>().Do(Handle)
                    .When<ClientMessage.TransactionStart>().Do(Handle)
                    .When<ClientMessage.TransactionWrite>().Do(Handle)
                    .When<ClientMessage.TransactionCommit>().Do(Handle)
                    .When<ClientMessage.DeleteStream>().Do(Handle)
                    .WhenOther().Do(m => _outputBus.Publish(m))
                .InStates(VNodeState.Initializing, VNodeState.Master)
                    .When<ClientMessage.RequestShutdown>().Do(Handle)
                .InStates(VNodeState.ShuttingDown, VNodeState.Shutdown)
                    .When<SystemMessage.ServiceShutdown>().Do(Handle)
                // TODO AN reply with correct status code, that system is shutting down (or already shut down)
                    .When<ClientMessage.WriteEvents>().Ignore()
                    .When<ClientMessage.TransactionStart>().Ignore()
                    .When<ClientMessage.TransactionWrite>().Ignore()
                    .When<ClientMessage.TransactionCommit>().Ignore()
                    .When<ClientMessage.DeleteStream>().Ignore()
                    .When<ClientMessage.ReadEvent>().Ignore()
                    .When<ClientMessage.ReadStreamEventsBackward>().Ignore()
                    .When<ClientMessage.ReadStreamEventsForward>().Ignore()
                    .When<ClientMessage.ReadAllEventsForward>().Ignore()
                    .When<ClientMessage.ListStreams>().Ignore()
                    .WhenOther().Do(m => _outputBus.Publish(m))
                .InState(VNodeState.ShuttingDown)
                    .When<SystemMessage.ShutdownTimeout>().Do(Handle)
                .Build();
            return stm;
        }

        void IHandle<Message>.Handle(Message message)
        {
            _fsm.Handle(message);
        }

        private void Handle(SystemMessage.SystemInit message)
        {
            Log.Info("========= SystemInit: SingleVNodeController =========");
            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.SystemStart message)
        {
            Log.Info("========= SystemStart: SingleVNodeController =========");
            _outputBus.Publish(message);

            _mainQueue.Publish(new SystemMessage.BecomeWorking());
        }

        private void Handle(SystemMessage.BecomeWorking message)
        {
            Log.Info("[{0}] IS WORKING!!! SPARTA!!!111", _httpEndPoint);

            _state = VNodeState.Master;

            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.BecomeShuttingDown message)
        {
            Log.Info("[{0}] IS SHUTTING DOWN!!! FAREWELL, WORLD...", _httpEndPoint);

            _state = VNodeState.ShuttingDown;

            _mainQueue.Publish(TimerMessage.Schedule.Create(ShutdownTimeout,
                                                            new PublishEnvelope(_mainQueue),
                                                            new SystemMessage.ShutdownTimeout()));
            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.BecomeShutdown message)
        {
            Log.Info("[{0}] IS SHUT DOWN!!! SWEET DREAMS!!!111", _httpEndPoint);

            _state = VNodeState.Shutdown;

            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.StorageReaderInitializationDone message)
        {
            _storageReaderInitialized = true;
            _outputBus.Publish(message);

            CheckInitializationDone();
        }

        private void Handle(SystemMessage.StorageWriterInitializationDone message)
        {
            _storageWriterInitialized = true;
            _outputBus.Publish(message);

            CheckInitializationDone();
        }

        private void CheckInitializationDone()
        {
            if (_storageReaderInitialized && _storageWriterInitialized)
                _mainQueue.Publish(new SystemMessage.SystemStart());
        }

        private void Handle(ClientMessage.CreateStream message)
        {
            _outputBus.Publish(new ReplicationMessage.CreateStreamRequestCreated(message.CorrelationId,
                                                                                 message.Envelope,
                                                                                 message.EventStreamId,
                                                                                 message.Metadata));
        }

        private void Handle(ClientMessage.WriteEvents message)
        {
            _outputBus.Publish(new ReplicationMessage.WriteRequestCreated(message.CorrelationId,
                                                                          message.Envelope,
                                                                          message.EventStreamId,
                                                                          message.ExpectedVersion,
                                                                          message.Events));
        }

        private void Handle(ClientMessage.TransactionStart message)
        {
            _outputBus.Publish(new ReplicationMessage.TransactionStartRequestCreated(message.CorrelationId,
                                                                                     message.Envelope,
                                                                                     message.EventStreamId,
                                                                                     message.ExpectedVersion));
        }

        private void Handle(ClientMessage.TransactionWrite message)
        {
            _outputBus.Publish(new ReplicationMessage.TransactionWriteRequestCreated(message.CorrelationId,
                                                                                     message.Envelope,
                                                                                     message.TransactionId,
                                                                                     message.EventStreamId,
                                                                                     message.Events));
        }

        private void Handle(ClientMessage.TransactionCommit message)
        {
            _outputBus.Publish(new ReplicationMessage.TransactionCommitRequestCreated(message.CorrelationId,
                                                                                      message.Envelope,
                                                                                      message.TransactionId,
                                                                                      message.EventStreamId));
        }

        private void Handle(ClientMessage.DeleteStream message)
        {
            _outputBus.Publish(new ReplicationMessage.DeleteStreamRequestCreated(message.CorrelationId,
                                                                                 message.Envelope,
                                                                                 message.EventStreamId,
                                                                                 message.ExpectedVersion));
        }

        private void Handle(ClientMessage.RequestShutdown message)
        {
            _mainQueue.Publish(new SystemMessage.BecomeShuttingDown());
        }

        private void Handle(SystemMessage.ServiceShutdown message)
        {
            Log.Info("Service [{0}] has shut down.", message.ServiceName);

            --_serviceShutdownsToExpect;
            if (_serviceShutdownsToExpect == 0)
            {
                Log.Info("========== All Services Shutdown: SingleVNodeController =========");
                Shutdown();
            }
        }

        private void Handle(SystemMessage.ShutdownTimeout message)
        {
            if (_state != VNodeState.ShuttingDown)
                return;

            Log.Info("========== Shutdown Timeout: SingleVNodeController =========");
            Shutdown();
        }

        private void Shutdown()
        {
            _mainQueue.Publish(new SystemMessage.BecomeShutdown());
        }
    }
}
