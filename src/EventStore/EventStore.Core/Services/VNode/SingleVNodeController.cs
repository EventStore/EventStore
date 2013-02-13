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
using System.Net;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Services.VNode
{
    /// <summary>
    /// Implements finite state machine transitions for the Single VNode configuration.
    /// Also maps certain client messages to request messages. 
    /// </summary>
    public class SingleVNodeController : IHandle<Message>
    {
        public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

        private static readonly ILogger Log = LogManager.GetLoggerFor<SingleVNodeController>();

        private readonly IPublisher _outputBus;
        private readonly IPEndPoint _httpEndPoint;
        private readonly TFChunkDb _db;

        private VNodeState _state = VNodeState.Initializing;
        private QueuedHandler _mainQueue;
        private readonly VNodeFSM _fsm;

        private bool _storageReaderInitialized;
        private bool _storageWriterInitialized;
        private int _serviceShutdownsToExpect = 3;
        private bool _exitProcessOnShutdown;

        public SingleVNodeController(IPublisher outputBus, IPEndPoint httpEndPoint, TFChunkDb db)
        {
            Ensure.NotNull(outputBus, "outputBus");
            Ensure.NotNull(httpEndPoint, "httpEndPoint");
            Ensure.NotNull(db, "db");

            _outputBus = outputBus;
            _httpEndPoint = httpEndPoint;
            _db = db;

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
                    .When<SystemMessage.BecomePreMaster>().Do(Handle)
                    .When<SystemMessage.BecomeShutdown>().Do(Handle)
                .InState(VNodeState.Initializing)
                    .When<SystemMessage.StorageReaderInitializationDone>().Do(Handle)
                    .When<SystemMessage.StorageWriterInitializationDone>().Do(Handle)
                    .When<ClientMessage.ReadRequestMessage>().Ignore()
                    .When<ClientMessage.WriteRequestMessage>().Ignore()
                    .WhenOther().ForwardTo(_outputBus)
                .InState(VNodeState.PreMaster)
                    .When<SystemMessage.BecomeMaster>().Do(Handle)
                    .When<SystemMessage.WaitForChaserToCatchUp>().ForwardTo(_outputBus)
                    .When<SystemMessage.ChaserCaughtUp>().Do(Handle)
                    .When<ClientMessage.CreateStream>().Ignore()
                    .When<ClientMessage.WriteEvents>().Ignore()
                    .When<ClientMessage.TransactionStart>().Ignore()
                    .When<ClientMessage.TransactionWrite>().Ignore()
                    .When<ClientMessage.TransactionCommit>().Ignore()
                    .When<ClientMessage.DeleteStream>().Ignore()
                    .WhenOther().ForwardTo(_outputBus)
                .InAllStatesExcept(VNodeState.PreMaster)
                    .When<SystemMessage.WaitForChaserToCatchUp>().Ignore()
                    .When<SystemMessage.ChaserCaughtUp>().Ignore()
                .InAllStatesExcept(VNodeState.Master)
                    .When<StorageMessage.WritePrepares>().Ignore()
                    .When<StorageMessage.WriteDelete>().Ignore()
                    .When<StorageMessage.WriteTransactionStart>().Ignore()
                    .When<StorageMessage.WriteTransactionData>().Ignore()
                    .When<StorageMessage.WriteTransactionPrepare>().Ignore()
                    .When<StorageMessage.WriteCommit>().Ignore()
                .InState(VNodeState.Master)
                    .When<ClientMessage.CreateStream>().Do(Handle)
                    .When<ClientMessage.WriteEvents>().Do(Handle)
                    .When<ClientMessage.TransactionStart>().Do(Handle)
                    .When<ClientMessage.TransactionWrite>().Do(Handle)
                    .When<ClientMessage.TransactionCommit>().Do(Handle)
                    .When<ClientMessage.DeleteStream>().Do(Handle)
                    .WhenOther().ForwardTo(_outputBus)
                .InStates(VNodeState.Initializing, VNodeState.PreMaster, VNodeState.Master)
                    .When<ClientMessage.RequestShutdown>().Do(Handle)
                .InStates(VNodeState.ShuttingDown, VNodeState.Shutdown)
                    .When<SystemMessage.ServiceShutdown>().Do(Handle)
                // TODO AN reply with correct status code, that system is shutting down (or already shut down)
                    .When<ClientMessage.ReadRequestMessage>().Ignore()
                    .When<ClientMessage.WriteRequestMessage>().Ignore()
                    .WhenOther().ForwardTo(_outputBus)
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
            Log.Info("========== [{0}] SYSTEM INIT...", _httpEndPoint);
            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.SystemStart message)
        {
            Log.Info("========== [{0}] SYSTEM START....", _httpEndPoint);
            _outputBus.Publish(message);
            _mainQueue.Publish(new SystemMessage.BecomePreMaster(Guid.NewGuid()));
        }

        private void Handle(SystemMessage.BecomePreMaster message)
        {
            Log.Info("========== [{0}] PRE-MASTER STATE, WAITING FOR CHASER To CATCH UP...", _httpEndPoint);
            _state = VNodeState.PreMaster;
            _mainQueue.Publish(new SystemMessage.WaitForChaserToCatchUp(Guid.NewGuid(), TimeSpan.Zero));
            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.BecomeMaster message)
        {
            Log.Info("========== [{0}] IS WORKING!!! SPARTA!!!", _httpEndPoint);
            _state = VNodeState.Master;
            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.BecomeShuttingDown message)
        {
            if (_state == VNodeState.ShuttingDown || _state == VNodeState.Shutdown)
                return;

            Log.Info("========== [{0}] IS SHUTTING DOWN!!! FAREWELL, WORLD...", _httpEndPoint);
            _exitProcessOnShutdown = message.ExitProcess;
            _state = VNodeState.ShuttingDown;
            _mainQueue.Publish(TimerMessage.Schedule.Create(ShutdownTimeout, new PublishEnvelope(_mainQueue), new SystemMessage.ShutdownTimeout()));
            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.BecomeShutdown message)
        {
            Log.Info("========== [{0}] IS SHUT DOWN!!! SWEET DREAMS!!!", _httpEndPoint);

            _state = VNodeState.Shutdown;

            _outputBus.Publish(message);

            if (_exitProcessOnShutdown)
                Application.Exit(ExitCode.Success, "Shutdown with exiting from process was requested.");
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

        private void Handle(SystemMessage.ChaserCaughtUp message)
        {
            _outputBus.Publish(message);
            _mainQueue.Publish(new SystemMessage.BecomeMaster(Guid.NewGuid()));   
        }

        private void Handle(ClientMessage.CreateStream message)
        {
            _outputBus.Publish(new StorageMessage.CreateStreamRequestCreated(message.CorrelationId,
                                                                             message.Envelope,
                                                                             message.EventStreamId,
                                                                             message.RequestId,
                                                                             message.IsJson,
                                                                             message.Metadata));
        }

        private void Handle(ClientMessage.WriteEvents message)
        {
            _outputBus.Publish(new StorageMessage.WriteRequestCreated(message.CorrelationId,
                                                                      message.Envelope,
                                                                      message.EventStreamId,
                                                                      message.ExpectedVersion,
                                                                      message.Events));
        }

        private void Handle(ClientMessage.TransactionStart message)
        {
            _outputBus.Publish(new StorageMessage.TransactionStartRequestCreated(message.CorrelationId,
                                                                                 message.Envelope,
                                                                                 message.EventStreamId,
                                                                                 message.ExpectedVersion));
        }

        private void Handle(ClientMessage.TransactionWrite message)
        {
            _outputBus.Publish(new StorageMessage.TransactionWriteRequestCreated(message.CorrelationId,
                                                                                 message.Envelope,
                                                                                 message.TransactionId,
                                                                                 message.Events));
        }

        private void Handle(ClientMessage.TransactionCommit message)
        {
            _outputBus.Publish(new StorageMessage.TransactionCommitRequestCreated(message.CorrelationId,
                                                                                  message.Envelope,
                                                                                  message.TransactionId));
        }

        private void Handle(ClientMessage.DeleteStream message)
        {
            _outputBus.Publish(new StorageMessage.DeleteStreamRequestCreated(message.CorrelationId,
                                                                             message.Envelope,
                                                                             message.EventStreamId,
                                                                             message.ExpectedVersion));
        }

        private void Handle(ClientMessage.RequestShutdown message)
        {
            _mainQueue.Publish(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), message.ExitProcess));
        }

        private void Handle(SystemMessage.ServiceShutdown message)
        {
            Log.Info("========== [{0}] Service '{1}' has shut down.", _httpEndPoint, message.ServiceName);

            --_serviceShutdownsToExpect;
            if (_serviceShutdownsToExpect == 0)
            {
                Log.Info("========== [{0}] All Services Shutdown.", _httpEndPoint);
                Shutdown();
            }
        }

        private void Handle(SystemMessage.ShutdownTimeout message)
        {
            if (_state == VNodeState.Shutdown)
                return;
            Debug.Assert(_state == VNodeState.ShuttingDown);

            Log.Info("========== [{0}] Shutdown Timeout.", _httpEndPoint);
            Shutdown();
        }

        private void Shutdown()
        {
            Debug.Assert(_state == VNodeState.ShuttingDown);

            _db.Close();
            _mainQueue.Publish(new SystemMessage.BecomeShutdown(Guid.NewGuid()));
        }
    }
}
