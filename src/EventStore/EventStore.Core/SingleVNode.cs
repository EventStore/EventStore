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
using System.IO;
using System.Net;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.DataStructures;
using EventStore.Core.Helpers;
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
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.Services.UserManagement;
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
                           NodeSubsystems[] enabledNodeSubsystems,
                           int memTableEntryCount = ESConsts.MemTableEntryCount)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(vNodeSettings, "vNodeSettings");

            _tcpEndPoint = vNodeSettings.ExternalTcpEndPoint;
            _httpEndPoint = vNodeSettings.ExternalHttpEndPoint;

            _mainBus = new InMemoryBus("MainBus");
            _controller = new SingleVNodeController(_mainBus, _httpEndPoint, db);
            _mainQueue = new QueuedHandler(_controller, "MainQueue");
            _controller.SetMainQueue(_mainQueue);

            _enabledNodeSubsystems = enabledNodeSubsystems;

            // MONITORING
            var monitoringInnerBus = new InMemoryBus("MonitoringInnerBus", watchSlowMsg: false);
            var monitoringRequestBus = new InMemoryBus("MonitoringRequestBus", watchSlowMsg: false);
            var monitoringQueue = new QueuedHandler(monitoringInnerBus, "MonitoringQueue", true, TimeSpan.FromMilliseconds(100));
            var monitoring = new MonitoringService(monitoringQueue, 
                                                   monitoringRequestBus, 
                                                   _mainQueue, 
                                                   db.Config.WriterCheckpoint, 
                                                   db.Config.Path, 
                                                   vNodeSettings.StatsPeriod, 
                                                   _httpEndPoint,
                                                   vNodeSettings.StatsStorage);
            _mainBus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.SystemInit, Message>());
            _mainBus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.StateChangeMessage, Message>());
            _mainBus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.BecomeShuttingDown, Message>());
            _mainBus.Subscribe(monitoringQueue.WidenFrom<ClientMessage.WriteEventsCompleted, Message>());
            monitoringInnerBus.Subscribe<SystemMessage.SystemInit>(monitoring);
            monitoringInnerBus.Subscribe<SystemMessage.StateChangeMessage>(monitoring);
            monitoringInnerBus.Subscribe<SystemMessage.BecomeShuttingDown>(monitoring);
            monitoringInnerBus.Subscribe<ClientMessage.WriteEventsCompleted>(monitoring);
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
                                          new LRUCache<string, StreamCacheInfo>(ESConsts.StreamMetadataCacheCapacity),
                                          Application.IsDefined(Application.AdditionalCommitChecks),
                                          Application.IsDefined(Application.InfiniteMetastreams) ? int.MaxValue : 1);
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
// ReSharper disable RedundantTypeArgumentsOfMethod
            _mainBus.Subscribe<SystemMessage.ScavengeDatabase>(storageScavenger);
// ReSharper restore RedundantTypeArgumentsOfMethod

            // NETWORK SEND
            _networkSendService = new NetworkSendService(tcpQueueCount: vNodeSettings.TcpSendingThreads, httpQueueCount: vNodeSettings.HttpSendingThreads);

            // TCP
            var tcpService = new TcpService(_mainQueue, _tcpEndPoint, _networkSendService, 
                                            TcpServiceType.External, TcpSecurityType.Normal, new ClientTcpDispatcher(), 
                                            ESConsts.ExternalHeartbeatInterval, ESConsts.ExternalHeartbeatTimeout,
                                            null);
            _mainBus.Subscribe<SystemMessage.SystemInit>(tcpService);
            _mainBus.Subscribe<SystemMessage.SystemStart>(tcpService);
            _mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(tcpService);

            // HTTP
            var passwordHashAlgorithm = new Rfc2898PasswordHashAlgorithm();
            {
                var queues = new IQueuedHandler[vNodeSettings.HttpReceivingThreads];
                var buses = new InMemoryBus[vNodeSettings.HttpReceivingThreads];
                var multiQueuedHandler = new MultiQueuedHandler(queues, null);
                for (var i = 0; i < vNodeSettings.HttpReceivingThreads; i++)
                {
                    buses[i] = new InMemoryBus(string.Format("Incoming HTTP #{0} Bus", i + 1), watchSlowMsg: false);
                    queues[i] = new QueuedHandlerThreadPool(
                        buses[i], name: "Incoming HTTP #" + (i + 1), groupName: "Incoming HTTP", 
                        watchSlowMsg: true, slowMsgThreshold: TimeSpan.FromMilliseconds(50));
                }

                var dispatcher = new IODispatcher(_mainQueue, new PublishEnvelope(multiQueuedHandler, crossThread: true));
                var internalAuthenticationProvider = new InternalAuthenticationProvider(dispatcher, passwordHashAlgorithm, ESConsts.CachedPrincipalCount);

                var authenticationProviders = new AuthenticationProvider[]
                {
                    new BasicHttpAuthenticationProvider(internalAuthenticationProvider),
                    new TrustedAuthenticationProvider(), 
                    new AnonymousAuthenticationProvider()
                };

                _httpService = new HttpService(ServiceAccessibility.Public, _mainQueue, new TrieUriRouter(), 
                                               multiQueuedHandler, authenticationProviders, vNodeSettings.HttpPrefixes);
                _httpService.SetupController(new AdminController(_mainQueue));
                _httpService.SetupController(new PingController());
                _httpService.SetupController(new StatController(monitoringQueue, _networkSendService));
                _httpService.SetupController(new AtomController(_mainQueue, _networkSendService));
                _httpService.SetupController(new UsersController(_mainQueue, _networkSendService));

                _mainBus.Subscribe<SystemMessage.SystemInit>(_httpService);
                _mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(_httpService);
                _mainBus.Subscribe<HttpMessage.SendOverHttp>(_httpService);
                _mainBus.Subscribe<HttpMessage.PurgeTimedOutRequests>(_httpService);

                for (var i = 0; i < vNodeSettings.HttpReceivingThreads; i++)
                {
                    var bus = buses[i];

                    bus.Subscribe(dispatcher.ForwardReader);
                    bus.Subscribe(dispatcher.BackwardReader);
                    bus.Subscribe(dispatcher.Writer);
                    bus.Subscribe(dispatcher.StreamDeleter);

                    _httpService.CreateAndSubscribePipeline(bus);
                }
            }

            // REQUEST MANAGEMENT
            var requestManagement = new RequestManagementService(_mainQueue, 1, 1, vNodeSettings.PrepareTimeout, vNodeSettings.CommitTimeout);
            _mainBus.Subscribe<SystemMessage.SystemInit>(requestManagement);
            _mainBus.Subscribe<ClientMessage.WriteEvents>(requestManagement);
            _mainBus.Subscribe<ClientMessage.TransactionStart>(requestManagement);
            _mainBus.Subscribe<ClientMessage.TransactionWrite>(requestManagement);
            _mainBus.Subscribe<ClientMessage.TransactionCommit>(requestManagement);
            _mainBus.Subscribe<ClientMessage.DeleteStream>(requestManagement);
            _mainBus.Subscribe<StorageMessage.RequestCompleted>(requestManagement);
            _mainBus.Subscribe<StorageMessage.CheckStreamAccessCompleted>(requestManagement);
            _mainBus.Subscribe<StorageMessage.AlreadyCommitted>(requestManagement);
            _mainBus.Subscribe<StorageMessage.CommitAck>(requestManagement);
            _mainBus.Subscribe<StorageMessage.PrepareAck>(requestManagement);
            _mainBus.Subscribe<StorageMessage.WrongExpectedVersion>(requestManagement);
            _mainBus.Subscribe<StorageMessage.InvalidTransaction>(requestManagement);
            _mainBus.Subscribe<StorageMessage.StreamDeleted>(requestManagement);
            _mainBus.Subscribe<StorageMessage.RequestManagerTimerTick>(requestManagement);

            new SubscriptionsService(_mainBus, readIndex); // subscribes internally

            // USER MANAGEMENT
            var ioDispatcher = new IODispatcher(_mainQueue, new PublishEnvelope(_mainQueue));
            _mainBus.Subscribe(ioDispatcher.BackwardReader);
            _mainBus.Subscribe(ioDispatcher.ForwardReader);
            _mainBus.Subscribe(ioDispatcher.Writer);
            _mainBus.Subscribe(ioDispatcher.StreamDeleter);

            var userManagement = new UserManagementService(_mainQueue, ioDispatcher, passwordHashAlgorithm);
            _mainBus.Subscribe<UserManagementMessage.Create>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.Update>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.Enable>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.Disable>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.Delete>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.ResetPassword>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.ChangePassword>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.Get>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.GetAll>(userManagement);

            // TIMER
            _timerService = new TimerService(new ThreadBasedScheduler(new RealTimeProvider()));
            _mainBus.Subscribe<TimerMessage.Schedule>(_timerService);

            monitoringQueue.Start();
            _mainQueue.Start();
        }

        public void Start()
        {
            _mainQueue.Publish(new SystemMessage.SystemInit());
        }

        public void Stop(bool exitProcess)
        {
            _mainQueue.Publish(new ClientMessage.RequestShutdown(exitProcess));
        }

        public override string ToString()
        {
            return string.Format("[{0}, {1}]", _tcpEndPoint, _httpEndPoint);
        }
    }
}
