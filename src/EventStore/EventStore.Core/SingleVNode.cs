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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Core.Authentication;
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
using EventStore.Core.TransactionLog;
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
        public IPublisher NetworkSendService { get { return _workersHandler; } }

        internal MultiQueuedHandler WorkersHandler { get { return _workersHandler; } }

        private readonly SingleVNodeSettings _settings;
        private readonly QueuedHandler _mainQueue;
        private readonly InMemoryBus _mainBus;

        private readonly SingleVNodeController _controller;
        private readonly HttpService _httpService;
        private readonly TimerService _timerService;
        private readonly ITimeProvider _timeProvider;
        private readonly ISubsystem[] _subsystems;

        private readonly InMemoryBus[] _workerBuses;
        private readonly MultiQueuedHandler _workersHandler;

        public SingleVNode(TFChunkDb db, 
                           SingleVNodeSettings vNodeSettings, 
                           bool dbVerifyHashes, 
                           int memTableEntryCount, 
                           params ISubsystem[] subsystems)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(vNodeSettings, "vNodeSettings");

            _settings = vNodeSettings;

            _mainBus = new InMemoryBus("MainBus");
            _controller = new SingleVNodeController(_mainBus, _settings.ExternalHttpEndPoint, db, this);
            _mainQueue = new QueuedHandler(_controller, "MainQueue");
            _controller.SetMainQueue(_mainQueue);

            _subsystems = subsystems;

            // MONITORING
            var monitoringInnerBus = new InMemoryBus("MonitoringInnerBus", watchSlowMsg: false);
            var monitoringRequestBus = new InMemoryBus("MonitoringRequestBus", watchSlowMsg: false);
            var monitoringQueue = new QueuedHandlerThreadPool(monitoringInnerBus, "MonitoringQueue", true, TimeSpan.FromMilliseconds(100));
            var monitoring = new MonitoringService(monitoringQueue,
                                                   monitoringRequestBus,
                                                   _mainQueue,
                                                   db.Config.WriterCheckpoint,
                                                   db.Config.Path,
                                                   vNodeSettings.StatsPeriod,
                                                   _settings.ExternalHttpEndPoint,
                                                   vNodeSettings.StatsStorage);
            _mainBus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.SystemInit, Message>());
            _mainBus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.StateChangeMessage, Message>());
            _mainBus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.BecomeShuttingDown, Message>());
            _mainBus.Subscribe(monitoringQueue.WidenFrom<SystemMessage.BecomeShutdown, Message>());
            _mainBus.Subscribe(monitoringQueue.WidenFrom<ClientMessage.WriteEventsCompleted, Message>());
            monitoringInnerBus.Subscribe<SystemMessage.SystemInit>(monitoring);
            monitoringInnerBus.Subscribe<SystemMessage.StateChangeMessage>(monitoring);
            monitoringInnerBus.Subscribe<SystemMessage.BecomeShuttingDown>(monitoring);
            monitoringInnerBus.Subscribe<SystemMessage.BecomeShutdown>(monitoring);
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
            var readerPool = new ObjectPool<ITransactionFileReader>(
                "ReadIndex readers pool", ESConsts.PTableInitialReaderCount, ESConsts.PTableMaxReaderCount,
                () => new TFChunkReader(db, db.Config.WriterCheckpoint));
            var tableIndex = new TableIndex(indexPath,
                                            () => new HashListMemTable(maxSize: memTableEntryCount * 2),
                                            () => new TFReaderLease(readerPool),
                                            maxSizeForMemory: memTableEntryCount,
                                            maxTablesPerLevel: 2,
                                            inMem: db.Config.InMemDb);
            var hasher = new XXHashUnsafe();
            var readIndex = new ReadIndex(_mainQueue,
                                          readerPool,
                                          tableIndex,
                                          hasher,
                                          ESConsts.StreamInfoCacheCapacity,
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

            var storageWriter = new StorageWriterService(_mainQueue, _mainBus, _settings.MinFlushDelay,
                                                         db, writer, readIndex.IndexWriter, epochManager); // subscribes internally
            monitoringRequestBus.Subscribe<MonitoringMessage.InternalStatsRequest>(storageWriter);

            var storageReader = new StorageReaderService(_mainQueue, _mainBus, readIndex, ESConsts.StorageReaderThreadCount, db.Config.WriterCheckpoint);
            _mainBus.Subscribe<SystemMessage.SystemInit>(storageReader);
            _mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(storageReader);
            _mainBus.Subscribe<SystemMessage.BecomeShutdown>(storageReader);
            monitoringRequestBus.Subscribe<MonitoringMessage.InternalStatsRequest>(storageReader);

            var chaser = new TFChunkChaser(db, db.Config.WriterCheckpoint, db.Config.ChaserCheckpoint);
            var storageChaser = new StorageChaser(_mainQueue, db.Config.WriterCheckpoint, chaser, readIndex.IndexCommitter, epochManager);
            _mainBus.Subscribe<SystemMessage.SystemInit>(storageChaser);
            _mainBus.Subscribe<SystemMessage.SystemStart>(storageChaser);
            _mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(storageChaser);

            var storageScavenger = new StorageScavenger(db,
                                                        tableIndex,
                                                        hasher,
                                                        readIndex,
                                                        Application.IsDefined(Application.AlwaysKeepScavenged),
                                                        mergeChunks: !vNodeSettings.DisableScavengeMerging);
            // ReSharper disable RedundantTypeArgumentsOfMethod
            _mainBus.Subscribe<ClientMessage.ScavengeDatabase>(storageScavenger);
            // ReSharper restore RedundantTypeArgumentsOfMethod

            // MISC WORKERS
            _workerBuses = Enumerable.Range(0, vNodeSettings.WorkerThreads).Select(queueNum =>
                new InMemoryBus(string.Format("Worker #{0} Bus", queueNum + 1),
                                watchSlowMsg: true,
                                slowMsgThreshold: TimeSpan.FromMilliseconds(200))).ToArray();
            _workersHandler = new MultiQueuedHandler(
                    vNodeSettings.WorkerThreads,
                    queueNum => new QueuedHandlerThreadPool(_workerBuses[queueNum],
                                                            string.Format("Worker #{0}", queueNum + 1),
                                                            groupName: "Workers",
                                                            watchSlowMsg: true,
                                                            slowMsgThreshold: TimeSpan.FromMilliseconds(200)));

            // AUTHENTICATION INFRASTRUCTURE
            var passwordHashAlgorithm = new Rfc2898PasswordHashAlgorithm();
            var dispatcher = new IODispatcher(_mainQueue, new PublishEnvelope(_workersHandler, crossThread: true));
            var internalAuthenticationProvider = new InternalAuthenticationProvider(dispatcher, passwordHashAlgorithm, ESConsts.CachedPrincipalCount);
            var passwordChangeNotificationReader = new PasswordChangeNotificationReader(_mainQueue, dispatcher);
            _mainBus.Subscribe<SystemMessage.SystemStart>(passwordChangeNotificationReader);
            _mainBus.Subscribe<SystemMessage.BecomeShutdown>(passwordChangeNotificationReader);
            _mainBus.Subscribe(internalAuthenticationProvider);
            SubscribeWorkers(bus =>
            {
                bus.Subscribe(dispatcher.ForwardReader);
                bus.Subscribe(dispatcher.BackwardReader);
                bus.Subscribe(dispatcher.Writer);
                bus.Subscribe(dispatcher.StreamDeleter);
                bus.Subscribe(dispatcher);
            });

            // TCP
            {
                var tcpService = new TcpService(_mainQueue, _settings.ExternalTcpEndPoint, _workersHandler,
                                                TcpServiceType.External, TcpSecurityType.Normal, new ClientTcpDispatcher(),
                                                ESConsts.ExternalHeartbeatInterval, ESConsts.ExternalHeartbeatTimeout,
                                                internalAuthenticationProvider, null);
                _mainBus.Subscribe<SystemMessage.SystemInit>(tcpService);
                _mainBus.Subscribe<SystemMessage.SystemStart>(tcpService);
                _mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(tcpService);
            }

            // SECURE TCP
            if (vNodeSettings.ExternalSecureTcpEndPoint != null)
            {
                var secureTcpService = new TcpService(_mainQueue, _settings.ExternalSecureTcpEndPoint, _workersHandler,
                                                      TcpServiceType.External, TcpSecurityType.Secure, new ClientTcpDispatcher(),
                                                      ESConsts.ExternalHeartbeatInterval, ESConsts.ExternalHeartbeatTimeout,
                                                      internalAuthenticationProvider, _settings.Certificate);
                _mainBus.Subscribe<SystemMessage.SystemInit>(secureTcpService);
                _mainBus.Subscribe<SystemMessage.SystemStart>(secureTcpService);
                _mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(secureTcpService);
            }

            SubscribeWorkers(bus =>
            {
                var tcpSendService = new TcpSendService();
                // ReSharper disable RedundantTypeArgumentsOfMethod
                bus.Subscribe<TcpMessage.TcpSend>(tcpSendService);
                // ReSharper restore RedundantTypeArgumentsOfMethod
            });

            // HTTP
            var httpAuthenticationProviders = new List<HttpAuthenticationProvider>
            {
                new BasicHttpAuthenticationProvider(internalAuthenticationProvider),
            };
            if (_settings.EnableTrustedAuth)
                httpAuthenticationProviders.Add(new TrustedHttpAuthenticationProvider());
            httpAuthenticationProviders.Add(new AnonymousHttpAuthenticationProvider());

            var httpPipe = new HttpMessagePipe();
            var httpSendService = new HttpSendService(httpPipe, forwardRequests: false);
            _mainBus.Subscribe<SystemMessage.StateChangeMessage>(httpSendService);
            _mainBus.Subscribe(new WideningHandler<HttpMessage.SendOverHttp, Message>(_workersHandler));
            SubscribeWorkers(bus =>
            {
                bus.Subscribe<HttpMessage.HttpSend>(httpSendService);
                bus.Subscribe<HttpMessage.HttpSendPart>(httpSendService);
                bus.Subscribe<HttpMessage.HttpBeginSend>(httpSendService);
                bus.Subscribe<HttpMessage.HttpEndSend>(httpSendService);
                bus.Subscribe<HttpMessage.SendOverHttp>(httpSendService);
            });

            _httpService = new HttpService(ServiceAccessibility.Public, _mainQueue, new TrieUriRouter(), 
                                            _workersHandler, vNodeSettings.HttpPrefixes);
            _httpService.SetupController(new AdminController(_mainQueue));
            _httpService.SetupController(new PingController());
            _httpService.SetupController(new StatController(monitoringQueue, _workersHandler));
            _httpService.SetupController(new AtomController(httpSendService, _mainQueue, _workersHandler));
            _httpService.SetupController(new GuidController(_mainQueue));
            _httpService.SetupController(new UsersController(httpSendService, _mainQueue, _workersHandler));

            _mainBus.Subscribe<SystemMessage.SystemInit>(_httpService);
            _mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(_httpService);
            _mainBus.Subscribe<HttpMessage.PurgeTimedOutRequests>(_httpService);

            SubscribeWorkers(bus =>
            {
                HttpService.CreateAndSubscribePipeline(bus, httpAuthenticationProviders.ToArray());
            });

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

            // SUBSCRIPTIONS
            var subscrBus = new InMemoryBus("SubscriptionsBus", true, TimeSpan.FromMilliseconds(50));
            var subscrQueue = new QueuedHandlerThreadPool(subscrBus, "Subscriptions", false);
            _mainBus.Subscribe(subscrQueue.WidenFrom<SystemMessage.SystemStart, Message>());
            _mainBus.Subscribe(subscrQueue.WidenFrom<SystemMessage.BecomeShuttingDown, Message>());
            _mainBus.Subscribe(subscrQueue.WidenFrom<TcpMessage.ConnectionClosed, Message>());
            _mainBus.Subscribe(subscrQueue.WidenFrom<ClientMessage.SubscribeToStream, Message>());
            _mainBus.Subscribe(subscrQueue.WidenFrom<ClientMessage.UnsubscribeFromStream, Message>());
            _mainBus.Subscribe(subscrQueue.WidenFrom<SubscriptionMessage.PollStream, Message>());
            _mainBus.Subscribe(subscrQueue.WidenFrom<SubscriptionMessage.CheckPollTimeout, Message>());
            _mainBus.Subscribe(subscrQueue.WidenFrom<StorageMessage.EventCommited, Message>());

            var subscription = new SubscriptionsService(_mainQueue, subscrQueue, readIndex);
            subscrBus.Subscribe<SystemMessage.SystemStart>(subscription);
            subscrBus.Subscribe<SystemMessage.BecomeShuttingDown>(subscription);
            subscrBus.Subscribe<TcpMessage.ConnectionClosed>(subscription);
            subscrBus.Subscribe<ClientMessage.SubscribeToStream>(subscription);
            subscrBus.Subscribe<ClientMessage.UnsubscribeFromStream>(subscription);
            subscrBus.Subscribe<SubscriptionMessage.PollStream>(subscription);
            subscrBus.Subscribe<SubscriptionMessage.CheckPollTimeout>(subscription);
            subscrBus.Subscribe<StorageMessage.EventCommited>(subscription);

            // USER MANAGEMENT
            var ioDispatcher = new IODispatcher(_mainQueue, new PublishEnvelope(_mainQueue));
            _mainBus.Subscribe(ioDispatcher.BackwardReader);
            _mainBus.Subscribe(ioDispatcher.ForwardReader);
            _mainBus.Subscribe(ioDispatcher.Writer);
            _mainBus.Subscribe(ioDispatcher.StreamDeleter);
            _mainBus.Subscribe(ioDispatcher);

            var userManagement = new UserManagementService(
                _mainQueue, ioDispatcher, passwordHashAlgorithm, vNodeSettings.SkipInitializeStandardUsersCheck);
            _mainBus.Subscribe<UserManagementMessage.Create>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.Update>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.Enable>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.Disable>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.Delete>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.ResetPassword>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.ChangePassword>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.Get>(userManagement);
            _mainBus.Subscribe<UserManagementMessage.GetAll>(userManagement);
            _mainBus.Subscribe<SystemMessage.BecomeMaster>(userManagement);

            // TIMER
            _timeProvider = new RealTimeProvider();
            _timerService = new TimerService(new ThreadBasedScheduler(_timeProvider));
            _mainBus.Subscribe<SystemMessage.BecomeShutdown>(_timerService);
            _mainBus.Subscribe<TimerMessage.Schedule>(_timerService);

            _workersHandler.Start();
            _mainQueue.Start();
            monitoringQueue.Start();
            subscrQueue.Start();

            if (subsystems != null)
                foreach (var subsystem in subsystems)
                    subsystem.Register(db, _mainQueue, _mainBus, _timerService, _timeProvider, httpSendService, new[]{_httpService}, _workersHandler);
        }

        private void SubscribeWorkers(Action<InMemoryBus> setup)
        {
            foreach (var workerBus in _workerBuses)
            {
                setup(workerBus);
            }
        }

        public void Start()
        {
            _mainQueue.Publish(new SystemMessage.SystemInit());
            //TODO: replace with messages
            if (_subsystems != null)
                foreach (var subsystem in _subsystems)
                    subsystem.Start();
        }

        public void Stop(bool exitProcess)
        {
            _mainQueue.Publish(new ClientMessage.RequestShutdown(exitProcess));
            //TODO: replace with messages
            if (_subsystems != null)
                foreach (var subsystem in _subsystems)
                    subsystem.Stop();
        }

        public override string ToString()
        {
            return string.Format("[{0}, {1}, {2}]",
                                 _settings.ExternalTcpEndPoint,
                                 _settings.ExternalSecureTcpEndPoint == null ? "n/a" : _settings.ExternalSecureTcpEndPoint.ToString(),
                                 _settings.ExternalHttpEndPoint);
        }
    }
}
