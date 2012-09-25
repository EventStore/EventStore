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
using System.IO;
using System.Net;
using EventStore.Common.Settings;
using EventStore.Core.Bus;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.Services.VNode;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core
{
    public class SingleVNode
    {
        private readonly IPEndPoint _tcpEndPoint;
        private readonly IPEndPoint _httpEndPoint;

        private readonly QueuedHandler _mainQueue;
        private readonly InMemoryBus _outputBus;

        private readonly SingleVNodeController _controller;
        private HttpService _httpService;
        private TimerService _timerService;

        public SingleVNode(TFChunkDb db, SingleVNodeSettings vNodeSettings, SingleVNodeAppSettings appSettings)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(vNodeSettings, "vNodeSettings");

            db.OpenVerifyAndClean();

            _tcpEndPoint = vNodeSettings.ExternalTcpEndPoint;
            _httpEndPoint = vNodeSettings.HttpEndPoint;

            _outputBus = new InMemoryBus("OutputBus");
            _controller = new SingleVNodeController(Bus, _httpEndPoint);
            _mainQueue = new QueuedHandler(_controller, "MainQueue");
            _controller.SetMainQueue(MainQueue);

            //MONITORING
            var monitoringInnerBus = new InMemoryBus("MonitoringInnerBus", watchSlowMsg: false);
            var monitoringRequestBus = new InMemoryBus("MonitoringRequestBus", watchSlowMsg: false);
            var monitoringQueue = new QueuedHandler(monitoringInnerBus, "MonitoringQueue", watchSlowMsg: true, slowMsgThresholdMs: 100);
            var monitoring = new MonitoringService(monitoringQueue, monitoringRequestBus, db.Config.WriterCheckpoint, appSettings.StatsPeriod);
            Bus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.SystemInit, Message>());
            Bus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.BecomeShuttingDown, Message>());
            monitoringInnerBus.Subscribe<SystemMessage.SystemInit>(monitoring);
            monitoringInnerBus.Subscribe<SystemMessage.BecomeShuttingDown>(monitoring);
            monitoringInnerBus.Subscribe<MonitoringMessage.GetFreshStats>(monitoring);

            //STORAGE SUBSYSTEM
            var indexPath = Path.Combine(db.Config.Path, "index");
            var tableIndex = new TableIndex(indexPath,
                                            () => new HashListMemTable(),
                                            new InMemoryCheckpoint(),
                                            maxSizeForMemory: 1000000,
                                            maxTablesPerLevel: 2);

            var readIndex = new ReadIndex(_mainQueue,
                                          pos => new TFChunkChaser(db, db.Config.WriterCheckpoint, new InMemoryCheckpoint(pos)),
                                          () => new TFChunkReader(db, db.Config.WriterCheckpoint),
                                          TFConsts.ReadIndexReaderCount,
                                          tableIndex,
                                          new XXHashUnsafe());
            var writer = new TFChunkWriter(db);
            var storageWriter = new StorageWriter(_mainQueue, _outputBus, writer, readIndex);
            var storageReader = new StorageReader(_mainQueue, _outputBus, readIndex, TFConsts.StorageReaderHandlerCount);
            monitoringRequestBus.Subscribe<MonitoringMessage.InternalStatsRequest>(storageReader);

            var chaser = new TFChunkChaser(db,
                                           db.Config.WriterCheckpoint,
                                           db.Config.GetNamedCheckpoint(Checkpoint.Chaser));
            var storageChaser = new StorageChaser(_mainQueue, chaser);
            _outputBus.Subscribe<SystemMessage.SystemInit>(storageChaser);
            _outputBus.Subscribe<SystemMessage.SystemStart>(storageChaser);
            _outputBus.Subscribe<SystemMessage.BecomeShuttingDown>(storageChaser);

            var storageScavenger = new StorageScavenger(db, readIndex);
            _outputBus.Subscribe<SystemMessage.ScavengeDatabase>(storageScavenger);

            //TCP
            var tcpService = new TcpService(MainQueue, _tcpEndPoint);
            Bus.Subscribe<SystemMessage.SystemInit>(tcpService);
            Bus.Subscribe<SystemMessage.SystemStart>(tcpService);
            Bus.Subscribe<SystemMessage.BecomeShuttingDown>(tcpService);

            //HTTP
            HttpService = new HttpService(MainQueue, vNodeSettings.HttpPrefixes);
            Bus.Subscribe<SystemMessage.SystemInit>(HttpService);
            Bus.Subscribe<SystemMessage.BecomeShuttingDown>(HttpService);
            Bus.Subscribe<HttpMessage.SendOverHttp>(HttpService);
            Bus.Subscribe<HttpMessage.UpdatePendingRequests>(HttpService);
            HttpService.SetupController(new AdminController(MainQueue));
            HttpService.SetupController(new PingController());
            HttpService.SetupController(new StatController(monitoringQueue));
            HttpService.SetupController(new ReadEventDataController(MainQueue));
            HttpService.SetupController(new AtomController(MainQueue));
            HttpService.SetupController(new WebSiteController(MainQueue));

            //REQUEST MANAGEMENT
            var requestManagement = new RequestManagementService(MainQueue, 1, 1);
            Bus.Subscribe<ReplicationMessage.EventCommited>(requestManagement);
            Bus.Subscribe<ReplicationMessage.CreateStreamRequestCreated>(requestManagement);
            Bus.Subscribe<ReplicationMessage.WriteRequestCreated>(requestManagement);
            Bus.Subscribe<ReplicationMessage.TransactionStartRequestCreated>(requestManagement);
            Bus.Subscribe<ReplicationMessage.TransactionWriteRequestCreated>(requestManagement);
            Bus.Subscribe<ReplicationMessage.TransactionCommitRequestCreated>(requestManagement);
            Bus.Subscribe<ReplicationMessage.DeleteStreamRequestCreated>(requestManagement);
            Bus.Subscribe<ReplicationMessage.RequestCompleted>(requestManagement);
            Bus.Subscribe<ReplicationMessage.CommitAck>(requestManagement);
            Bus.Subscribe<ReplicationMessage.PrepareAck>(requestManagement);
            Bus.Subscribe<ReplicationMessage.WrongExpectedVersion>(requestManagement);
            Bus.Subscribe<ReplicationMessage.InvalidTransaction>(requestManagement);
            Bus.Subscribe<ReplicationMessage.StreamDeleted>(requestManagement);
            Bus.Subscribe<ReplicationMessage.PreparePhaseTimeout>(requestManagement);
            Bus.Subscribe<ReplicationMessage.CommitPhaseTimeout>(requestManagement);

            var clientService = new ClientService();
            Bus.Subscribe<TcpMessage.ConnectionClosed>(clientService);
            Bus.Subscribe<ClientMessage.SubscribeToStream>(clientService);
            Bus.Subscribe<ClientMessage.UnsubscribeFromStream>(clientService);
            Bus.Subscribe<ClientMessage.SubscribeToAllStreams>(clientService);
            Bus.Subscribe<ClientMessage.UnsubscribeFromAllStreams>(clientService);
            Bus.Subscribe<ReplicationMessage.EventCommited>(clientService);

            //TIMER
            //var timer = new TimerService(new TimerBasedScheduler(new RealTimer(), new RealTimeProvider()));
            TimerService = new TimerService(new ThreadBasedScheduler(new RealTimeProvider()));
            Bus.Subscribe<TimerMessage.Schedule>(TimerService);

            MainQueue.Start();
            monitoringQueue.Start();
        }

        public QueuedHandler MainQueue
        {
            get { return _mainQueue; }
        }

        public InMemoryBus Bus
        {
            get { return _outputBus; }
        }

        public HttpService HttpService
        {
            get { return _httpService; }
            set { _httpService = value; }
        }

        public TimerService TimerService
        {
            get { return _timerService; }
            set { _timerService = value; }
        }

        public void Start()
        {
            MainQueue.Publish(new SystemMessage.SystemInit());
        }

        public void Stop()
        {
            MainQueue.Publish(new SystemMessage.BecomeShuttingDown());
        }

        public override string ToString()
        {
            return string.Format("[{0}, {1}]", _tcpEndPoint, _httpEndPoint);
        }
    }
}
