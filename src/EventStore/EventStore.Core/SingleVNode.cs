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
using System.IO;
using System.Net;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.Services.VNode;
using EventStore.Common.Utils;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core
{
    public class SingleVNode
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<SingleVNode>();

        public QueuedHandler MainQueue { get { return _mainQueue; } }
        public ISubscriber MainBus { get { return _mainBus; } } 
        public HttpService HttpService { get { return _httpService; } }
        public TimerService TimerService { get { return _timerService; } }
        public IPublisher NetworkSendService { get { return _networkSendService; } }

        private readonly IPEndPoint _tcpEndPoint;
        private readonly IPEndPoint _httpEndPoint;

        private readonly QueuedHandler _mainQueue;
        private readonly InMemoryBus _mainBus;

        private readonly SingleVNodeController _controller;
        private readonly HttpService _httpService;
        private readonly TimerService _timerService;
        private readonly NetworkSendService _networkSendService;

        private readonly NodeSubsystems[] _enabledNodeSubsystems;
        
        public SingleVNode(TFChunkDb db, 
                           SingleVNodeSettings vNodeSettings, 
                           bool dbVerifyHashes,
                           bool runProjections,
                           int memTableEntryCount = ESConsts.MemTableEntryCount)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(vNodeSettings, "vNodeSettings");

            _tcpEndPoint = vNodeSettings.ExternalTcpEndPoint;
            _httpEndPoint = vNodeSettings.ExternalHttpEndPoint;

            _mainBus = new InMemoryBus("MainBus");
            _controller = new SingleVNodeController(_mainBus, _httpEndPoint, db);
            _mainQueue = new QueuedHandler(_controller, "MainQueue");
            _controller.SetMainQueue(MainQueue);

            _enabledNodeSubsystems = runProjections ? new [] { NodeSubsystems.Projections } : new NodeSubsystems[0];

            // MONITORING
            var monitoringInnerBus = new InMemoryBus("MonitoringInnerBus", watchSlowMsg: false);
            var monitoringRequestBus = new InMemoryBus("MonitoringRequestBus", watchSlowMsg: false);
            var monitoringQueue = new QueuedHandler(monitoringInnerBus, "MonitoringQueue", true, TimeSpan.FromMilliseconds(100));
            var monitoring = new MonitoringService(monitoringQueue, 
                                                   monitoringRequestBus, 
                                                   MainQueue, 
                                                   db.Config.WriterCheckpoint, 
                                                   db.Config.Path, 
                                                   vNodeSettings.StatsPeriod, 
                                                   _httpEndPoint,
                                                   vNodeSettings.StatsStorage);
            _mainBus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.SystemInit, Message>());
            _mainBus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.StateChangeMessage, Message>());
            _mainBus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.BecomeShuttingDown, Message>());
            _mainBus.Subscribe(monitoringQueue.WidenFrom<ClientMessage.CreateStreamCompleted, Message>());
            monitoringInnerBus.Subscribe<SystemMessage.SystemInit>(monitoring);
            monitoringInnerBus.Subscribe<SystemMessage.StateChangeMessage>(monitoring);
            monitoringInnerBus.Subscribe<SystemMessage.BecomeShuttingDown>(monitoring);
            monitoringInnerBus.Subscribe<ClientMessage.CreateStreamCompleted>(monitoring);
            monitoringInnerBus.Subscribe<MonitoringMessage.GetFreshStats>(monitoring);

            var truncPos = db.Config.TruncateCheckpoint.Read();
            if (truncPos != -1)
            {
                Log.Info("Truncate checkpoint is present. Truncate: {0} (0x{0:X}), Writer: {1} (0x{1:X}), Chaser: {2} (0x{2:X}), Epoch: {3} (0x{3:X})",
                         truncPos, db.Config.WriterCheckpoint.Read(), db.Config.ChaserCheckpoint.Read(), db.Config.EpochCheckpoint.Read());
                var truncator = new TFChunkDbTruncator(db.Config);
                truncator.TruncateDb(truncPos);
            }

            db.Open(dbVerifyHashes);

            // STORAGE SUBSYSTEM
            var indexPath = Path.Combine(db.Config.Path, "index");
            var tableIndex = new TableIndex(indexPath,
                                            () => new HashListMemTable(maxSize: memTableEntryCount * 2),
                                            maxSizeForMemory: memTableEntryCount,
                                            maxTablesPerLevel: 2);

            var readIndex = new ReadIndex(_mainQueue, 
                                          ESConsts.PTableInitialReaderCount, 
                                          ESConsts.PTableMaxReaderCount, 
                                          () => new TFChunkReader(db, db.Config.WriterCheckpoint), 
                                          tableIndex, 
                                          new XXHashUnsafe(),
                                          new LRUCache<string, StreamCacheInfo>(ESConsts.StreamMetadataCacheCapacity));
            var writer = new TFChunkWriter(db);
            var epochManager = new EpochManager(ESConsts.CachedEpochCount,
                                                db.Config.EpochCheckpoint,
                                                writer,
                                                initialReaderCount: 1,
                                                maxReaderCount: 5,
                                                readerFactory: () => new TFChunkReader(db, db.Config.WriterCheckpoint));
            epochManager.Init();
            new StorageWriterService(_mainQueue, _mainBus, db, writer, readIndex, epochManager); // subscribes internally
            var storageReader = new StorageReaderService(_mainQueue, _mainBus, readIndex, ESConsts.StorageReaderThreadCount, db.Config.WriterCheckpoint);
            _mainBus.Subscribe<SystemMessage.SystemInit>(storageReader);
            _mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(storageReader);
            _mainBus.Subscribe<SystemMessage.BecomeShutdown>(storageReader);
            monitoringRequestBus.Subscribe<MonitoringMessage.InternalStatsRequest>(storageReader);

            var chaser = new TFChunkChaser(db, db.Config.WriterCheckpoint, db.Config.ChaserCheckpoint);
            var storageChaser = new StorageChaser(_mainQueue, db.Config.WriterCheckpoint, chaser, readIndex, epochManager, _tcpEndPoint);
            _mainBus.Subscribe<SystemMessage.SystemInit>(storageChaser);
            _mainBus.Subscribe<SystemMessage.SystemStart>(storageChaser);
            _mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(storageChaser);

            var storageScavenger = new StorageScavenger(db,
                                                        readIndex,
                                                        Application.IsDefined(Application.AlwaysKeepScavenged),
                                                        !Application.IsDefined(Application.DisableMergeChunks));
            _mainBus.Subscribe<SystemMessage.ScavengeDatabase>(storageScavenger);

            // NETWORK SEND
            _networkSendService = new NetworkSendService(tcpQueueCount: vNodeSettings.TcpSendingThreads, httpQueueCount: vNodeSettings.HttpSendingThreads);

            // TCP
            var tcpService = new TcpService(MainQueue, _tcpEndPoint, _networkSendService, TcpServiceType.External, new ClientTcpDispatcher());
            _mainBus.Subscribe<SystemMessage.SystemInit>(tcpService);
            _mainBus.Subscribe<SystemMessage.SystemStart>(tcpService);
            _mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(tcpService);

            // HTTP
            _httpService = new HttpService(ServiceAccessibility.Private, MainQueue, vNodeSettings.HttpReceivingThreads, vNodeSettings.HttpPrefixes);
            _mainBus.Subscribe<SystemMessage.SystemInit>(HttpService);
            _mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(HttpService);
            _mainBus.Subscribe<HttpMessage.SendOverHttp>(HttpService);
            _mainBus.Subscribe<HttpMessage.PurgeTimedOutRequests>(HttpService);
            HttpService.SetupController(new AdminController(MainQueue));
            HttpService.SetupController(new PingController());
            HttpService.SetupController(new StatController(monitoringQueue, _networkSendService));
            HttpService.SetupController(new ReadEventDataController(MainQueue, _networkSendService));
            HttpService.SetupController(new AtomController(MainQueue, _networkSendService));
            HttpService.SetupController(new WebSiteController(MainQueue, _enabledNodeSubsystems));

            // REQUEST MANAGEMENT
            var requestManagement = new RequestManagementService(MainQueue, 1, 1, vNodeSettings.PrepareTimeout, vNodeSettings.CommitTimeout);
            _mainBus.Subscribe<SystemMessage.SystemInit>(requestManagement);
            _mainBus.Subscribe<StorageMessage.CreateStreamRequestCreated>(requestManagement);
            _mainBus.Subscribe<StorageMessage.WriteRequestCreated>(requestManagement);
            _mainBus.Subscribe<StorageMessage.TransactionStartRequestCreated>(requestManagement);
            _mainBus.Subscribe<StorageMessage.TransactionWriteRequestCreated>(requestManagement);
            _mainBus.Subscribe<StorageMessage.TransactionCommitRequestCreated>(requestManagement);
            _mainBus.Subscribe<StorageMessage.DeleteStreamRequestCreated>(requestManagement);
            _mainBus.Subscribe<StorageMessage.RequestCompleted>(requestManagement);
            _mainBus.Subscribe<StorageMessage.AlreadyCommitted>(requestManagement);
            _mainBus.Subscribe<StorageMessage.CommitAck>(requestManagement);
            _mainBus.Subscribe<StorageMessage.PrepareAck>(requestManagement);
            _mainBus.Subscribe<StorageMessage.WrongExpectedVersion>(requestManagement);
            _mainBus.Subscribe<StorageMessage.InvalidTransaction>(requestManagement);
            _mainBus.Subscribe<StorageMessage.StreamDeleted>(requestManagement);
            _mainBus.Subscribe<StorageMessage.RequestManagerTimerTick>(requestManagement);

            new SubscriptionsService(_mainBus, readIndex); // subscribes internally

            // TIMER
            _timerService = new TimerService(new ThreadBasedScheduler(new RealTimeProvider()));
            _mainBus.Subscribe<TimerMessage.Schedule>(TimerService);

            monitoringQueue.Start();
            MainQueue.Start();
        }

        public void Start()
        {
            MainQueue.Publish(new SystemMessage.SystemInit());
        }

        public void Stop(bool exitProcess)
        {
            MainQueue.Publish(new ClientMessage.RequestShutdown(exitProcess));
        }

        public override string ToString()
        {
            return string.Format("[{0}, {1}]", _tcpEndPoint, _httpEndPoint);
        }
    }
}
