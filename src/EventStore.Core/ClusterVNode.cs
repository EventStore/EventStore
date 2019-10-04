using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Cluster.Settings;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.Gossip;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Services.Replication;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.Services.VNode;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Authentication;
using EventStore.Core.Helpers;
using EventStore.Core.Services.PersistentSubscription;
using System.Threading;
using EventStore.Core.Services.Histograms;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;
using System.Threading.Tasks;
using System.Collections;
using System.Diagnostics;

namespace EventStore.Core {
	public class ClusterVNode :
		IHandle<SystemMessage.StateChangeMessage>,
		IHandle<SystemMessage.BecomeShutdown> {
		private static readonly ILogger Log = LogManager.GetLoggerFor<ClusterVNode>();

		public IQueuedHandler MainQueue {
			get { return _mainQueue; }
		}

		public ISubscriber MainBus {
			get { return _mainBus; }
		}

		public HttpService InternalHttpService {
			get { return _internalHttpService; }
		}

		public HttpService ExternalHttpService {
			get { return _externalHttpService; }
		}

		public TimerService TimerService {
			get { return _timerService; }
		}

		public IPublisher NetworkSendService {
			get { return _workersHandler; }
		}

		public IAuthenticationProvider InternalAuthenticationProvider {
			get { return _internalAuthenticationProvider; }
		}

		internal MultiQueuedHandler WorkersHandler {
			get { return _workersHandler; }
		}

		private readonly VNodeInfo _nodeInfo;
		private readonly IQueuedHandler _mainQueue;
		private readonly ISubscriber _mainBus;

		private readonly ClusterVNodeController _controller;
		private readonly TimerService _timerService;
		private readonly HttpService _internalHttpService;
		private readonly HttpService _externalHttpService;
		private readonly ITimeProvider _timeProvider;
		private readonly ISubsystem[] _subsystems;
		private readonly ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
		private readonly IAuthenticationProvider _internalAuthenticationProvider;


		private readonly InMemoryBus[] _workerBuses;
		private readonly MultiQueuedHandler _workersHandler;
		public event EventHandler<VNodeStatusChangeArgs> NodeStatusChanged;
		private readonly List<Task> _tasks = new List<Task>();
		public IEnumerable<Task> Tasks {
			get { return _tasks; }
		}
#if DEBUG
		public TaskCompletionSource<bool> _taskAddedTrigger = new TaskCompletionSource<bool>();
		public object _taskAddLock = new object();
#endif

		protected virtual void OnNodeStatusChanged(VNodeStatusChangeArgs e) {
			EventHandler<VNodeStatusChangeArgs> handler = NodeStatusChanged;
			if (handler != null) handler(this, e);
		}


		public ClusterVNode(TFChunkDb db,
			ClusterVNodeSettings vNodeSettings,
			IGossipSeedSource gossipSeedSource,
			InfoController infoController,
			params ISubsystem[] subsystems) {
			Ensure.NotNull(db, "db");
			Ensure.NotNull(vNodeSettings, "vNodeSettings");
			Ensure.NotNull(gossipSeedSource, "gossipSeedSource");

#if DEBUG
			AddTask(_taskAddedTrigger.Task);
#endif

			var isSingleNode = vNodeSettings.ClusterNodeCount == 1;
			_nodeInfo = vNodeSettings.NodeInfo;
			_mainBus = new InMemoryBus("MainBus");

			var forwardingProxy = new MessageForwardingProxy();
			if (vNodeSettings.EnableHistograms) {
				HistogramService.CreateHistograms();
				//start watching jitter
				HistogramService.StartJitterMonitor();
			}

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

			_subsystems = subsystems;

			_controller = new ClusterVNodeController((IPublisher)_mainBus, _nodeInfo, db, vNodeSettings, this,
				forwardingProxy, _subsystems);
			_mainQueue = QueuedHandler.CreateQueuedHandler(_controller, "MainQueue");

			_controller.SetMainQueue(_mainQueue);

			//SELF
			_mainBus.Subscribe<SystemMessage.StateChangeMessage>(this);
			_mainBus.Subscribe<SystemMessage.BecomeShutdown>(this);
			// MONITORING
			var monitoringInnerBus = new InMemoryBus("MonitoringInnerBus", watchSlowMsg: false);
			var monitoringRequestBus = new InMemoryBus("MonitoringRequestBus", watchSlowMsg: false);
			var monitoringQueue = new QueuedHandlerThreadPool(monitoringInnerBus, "MonitoringQueue", true,
				TimeSpan.FromMilliseconds(800));
			var monitoring = new MonitoringService(monitoringQueue,
				monitoringRequestBus,
				_mainQueue,
				db.Config.WriterCheckpoint,
				db.Config.Path,
				vNodeSettings.StatsPeriod,
				_nodeInfo.ExternalHttp,
				vNodeSettings.StatsStorage,
				_nodeInfo.ExternalTcp,
				_nodeInfo.ExternalSecureTcp);
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
			monitoringInnerBus.Subscribe<MonitoringMessage.GetFreshTcpConnectionStats>(monitoring);

			var truncPos = db.Config.TruncateCheckpoint.Read();
			var writerCheckpoint = db.Config.WriterCheckpoint.Read();
			var chaserCheckpoint = db.Config.ChaserCheckpoint.Read();
			var epochCheckpoint = db.Config.EpochCheckpoint.Read();
			if (truncPos != -1) {
				Log.Info(
					"Truncate checkpoint is present. Truncate: {truncatePosition} (0x{truncatePosition:X}), Writer: {writerCheckpoint} (0x{writerCheckpoint:X}), Chaser: {chaserCheckpoint} (0x{chaserCheckpoint:X}), Epoch: {epochCheckpoint} (0x{epochCheckpoint:X})",
					truncPos, truncPos, writerCheckpoint, writerCheckpoint, chaserCheckpoint, chaserCheckpoint,
					epochCheckpoint, epochCheckpoint);
				var truncator = new TFChunkDbTruncator(db.Config);
				truncator.TruncateDb(truncPos);
			}

			// STORAGE SUBSYSTEM
			db.Open(vNodeSettings.VerifyDbHash, threads: vNodeSettings.InitializationThreads);
			var indexPath = vNodeSettings.Index ?? Path.Combine(db.Config.Path, "index");
			var maxReaderCount = ESConsts.PTableMaxReaderCount + vNodeSettings.ReaderThreadsCount;
			var readerPool = new ObjectPool<ITransactionFileReader>(
				"ReadIndex readers pool", ESConsts.PTableInitialReaderCount, maxReaderCount,
				() => new TFChunkReader(db, db.Config.WriterCheckpoint,
					optimizeReadSideCache: db.Config.OptimizeReadSideCache));
			var tableIndex = new TableIndex(indexPath,
				new XXHashUnsafe(),
				new Murmur3AUnsafe(),
				() => new HashListMemTable(vNodeSettings.IndexBitnessVersion,
					maxSize: vNodeSettings.MaxMemtableEntryCount * 2),
				() => new TFReaderLease(readerPool),
				vNodeSettings.IndexBitnessVersion,
				maxSizeForMemory: vNodeSettings.MaxMemtableEntryCount,
				maxTablesPerLevel: 2,
				inMem: db.Config.InMemDb,
				skipIndexVerify: vNodeSettings.SkipIndexVerify,
				indexCacheDepth: vNodeSettings.IndexCacheDepth,
				initializationThreads: vNodeSettings.InitializationThreads,
				additionalReclaim: false,
				maxAutoMergeIndexLevel: vNodeSettings.MaxAutoMergeIndexLevel);
			var readIndex = new ReadIndex(_mainQueue,
				readerPool,
				tableIndex,
				ESConsts.StreamInfoCacheCapacity,
				Application.IsDefined(Application.AdditionalCommitChecks),
				Application.IsDefined(Application.InfiniteMetastreams) ? int.MaxValue : 1,
				vNodeSettings.HashCollisionReadLimit,
				vNodeSettings.SkipIndexScanOnReads,
				db.Config.ReplicationCheckpoint);
			var writer = new TFChunkWriter(db);
			var epochManager = new EpochManager(_mainQueue,
				ESConsts.CachedEpochCount,
				db.Config.EpochCheckpoint,
				writer,
				initialReaderCount: 1,
				maxReaderCount: 5,
				readerFactory: () => new TFChunkReader(db, db.Config.WriterCheckpoint,
					optimizeReadSideCache: db.Config.OptimizeReadSideCache));
			epochManager.Init();

			var storageWriter = new ClusterStorageWriterService(_mainQueue, _mainBus, vNodeSettings.MinFlushDelay,
				db, writer, readIndex.IndexWriter, epochManager,
				() => readIndex.LastCommitPosition); // subscribes internally
			AddTasks(storageWriter.Tasks);

			monitoringRequestBus.Subscribe<MonitoringMessage.InternalStatsRequest>(storageWriter);

			var storageReader = new StorageReaderService(_mainQueue, _mainBus, readIndex,
				vNodeSettings.ReaderThreadsCount, db.Config.WriterCheckpoint);
			_mainBus.Subscribe<SystemMessage.SystemInit>(storageReader);
			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(storageReader);
			_mainBus.Subscribe<SystemMessage.BecomeShutdown>(storageReader);
			monitoringRequestBus.Subscribe<MonitoringMessage.InternalStatsRequest>(storageReader);

			var indexCommitterService = new IndexCommitterService(readIndex.IndexCommitter, _mainQueue,
				db.Config.ReplicationCheckpoint, db.Config.WriterCheckpoint, vNodeSettings.CommitAckCount, tableIndex);
			AddTask(indexCommitterService.Task);

			_mainBus.Subscribe<SystemMessage.StateChangeMessage>(indexCommitterService);
			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(indexCommitterService);
			_mainBus.Subscribe<StorageMessage.CommitAck>(indexCommitterService);
			_mainBus.Subscribe<ClientMessage.MergeIndexes>(indexCommitterService);

			var chaser = new TFChunkChaser(db, db.Config.WriterCheckpoint, db.Config.ChaserCheckpoint,
				db.Config.OptimizeReadSideCache);
			var storageChaser = new StorageChaser(_mainQueue, db.Config.WriterCheckpoint, chaser, indexCommitterService,
				epochManager);
			AddTask(storageChaser.Task);

#if DEBUG
			QueueStatsCollector.InitializeCheckpoints(
				_nodeInfo.DebugIndex, db.Config.WriterCheckpoint, db.Config.ChaserCheckpoint);
#endif
			_mainBus.Subscribe<SystemMessage.SystemInit>(storageChaser);
			_mainBus.Subscribe<SystemMessage.SystemStart>(storageChaser);
			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(storageChaser);

			// AUTHENTICATION INFRASTRUCTURE - delegate to plugins
			_internalAuthenticationProvider =
				vNodeSettings.AuthenticationProviderFactory.BuildAuthenticationProvider(_mainQueue, _mainBus,
					_workersHandler, _workerBuses);

			Ensure.NotNull(_internalAuthenticationProvider, "authenticationProvider");

			{
				// EXTERNAL TCP
				if (!vNodeSettings.DisableInsecureTCP) {
					var extTcpService = new TcpService(_mainQueue, _nodeInfo.ExternalTcp, _workersHandler,
						TcpServiceType.External, TcpSecurityType.Normal, new ClientTcpDispatcher(),
						vNodeSettings.ExtTcpHeartbeatInterval, vNodeSettings.ExtTcpHeartbeatTimeout,
						_internalAuthenticationProvider, null, vNodeSettings.ConnectionPendingSendBytesThreshold,
						vNodeSettings.ConnectionQueueSizeThreshold);
					_mainBus.Subscribe<SystemMessage.SystemInit>(extTcpService);
					_mainBus.Subscribe<SystemMessage.SystemStart>(extTcpService);
					_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(extTcpService);
				}

				// EXTERNAL SECURE TCP
				if (_nodeInfo.ExternalSecureTcp != null) {
					var extSecTcpService = new TcpService(_mainQueue, _nodeInfo.ExternalSecureTcp, _workersHandler,
						TcpServiceType.External, TcpSecurityType.Secure, new ClientTcpDispatcher(),
						vNodeSettings.ExtTcpHeartbeatInterval, vNodeSettings.ExtTcpHeartbeatTimeout,
						_internalAuthenticationProvider, vNodeSettings.Certificate,
						vNodeSettings.ConnectionPendingSendBytesThreshold, vNodeSettings.ConnectionQueueSizeThreshold);
					_mainBus.Subscribe<SystemMessage.SystemInit>(extSecTcpService);
					_mainBus.Subscribe<SystemMessage.SystemStart>(extSecTcpService);
					_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(extSecTcpService);
				}

				if (!isSingleNode) {
					// INTERNAL TCP
					if (!vNodeSettings.DisableInsecureTCP) {
						var intTcpService = new TcpService(_mainQueue, _nodeInfo.InternalTcp, _workersHandler,
							TcpServiceType.Internal, TcpSecurityType.Normal,
							new InternalTcpDispatcher(),
							vNodeSettings.IntTcpHeartbeatInterval, vNodeSettings.IntTcpHeartbeatTimeout,
							_internalAuthenticationProvider, null, ESConsts.UnrestrictedPendingSendBytes,
							ESConsts.MaxConnectionQueueSize);
						_mainBus.Subscribe<SystemMessage.SystemInit>(intTcpService);
						_mainBus.Subscribe<SystemMessage.SystemStart>(intTcpService);
						_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(intTcpService);
					}

					// INTERNAL SECURE TCP
					if (_nodeInfo.InternalSecureTcp != null) {
						var intSecTcpService = new TcpService(_mainQueue, _nodeInfo.InternalSecureTcp, _workersHandler,
							TcpServiceType.Internal, TcpSecurityType.Secure,
							new InternalTcpDispatcher(),
							vNodeSettings.IntTcpHeartbeatInterval, vNodeSettings.IntTcpHeartbeatTimeout,
							_internalAuthenticationProvider, vNodeSettings.Certificate,
							ESConsts.UnrestrictedPendingSendBytes,
							ESConsts.MaxConnectionQueueSize);
						_mainBus.Subscribe<SystemMessage.SystemInit>(intSecTcpService);
						_mainBus.Subscribe<SystemMessage.SystemStart>(intSecTcpService);
						_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(intSecTcpService);
					}
				}
			}

			SubscribeWorkers(bus => {
				var tcpSendService = new TcpSendService();
				// ReSharper disable RedundantTypeArgumentsOfMethod
				bus.Subscribe<TcpMessage.TcpSend>(tcpSendService);
				// ReSharper restore RedundantTypeArgumentsOfMethod
			});

			var httpAuthenticationProviders = new List<HttpAuthenticationProvider> {
				new BasicHttpAuthenticationProvider(_internalAuthenticationProvider),
			};
			if (vNodeSettings.EnableTrustedAuth)
				httpAuthenticationProviders.Add(new TrustedHttpAuthenticationProvider());
			httpAuthenticationProviders.Add(new AnonymousHttpAuthenticationProvider());

			var httpPipe = new HttpMessagePipe();
			var httpSendService = new HttpSendService(httpPipe, forwardRequests: true);
			_mainBus.Subscribe<SystemMessage.StateChangeMessage>(httpSendService);
			_mainBus.Subscribe(new WideningHandler<HttpMessage.SendOverHttp, Message>(_workersHandler));
			SubscribeWorkers(bus => {
				bus.Subscribe<HttpMessage.HttpSend>(httpSendService);
				bus.Subscribe<HttpMessage.HttpSendPart>(httpSendService);
				bus.Subscribe<HttpMessage.HttpBeginSend>(httpSendService);
				bus.Subscribe<HttpMessage.HttpEndSend>(httpSendService);
				bus.Subscribe<HttpMessage.SendOverHttp>(httpSendService);
			});

			_mainBus.Subscribe<SystemMessage.StateChangeMessage>(infoController);

			var adminController = new AdminController(_mainQueue, _workersHandler);
			var pingController = new PingController();
			var histogramController = new HistogramController();
			var statController = new StatController(monitoringQueue, _workersHandler);
			var atomController = new AtomController(httpSendService, _mainQueue, _workersHandler,
				vNodeSettings.DisableHTTPCaching);
			var gossipController = new GossipController(_mainQueue, _workersHandler, vNodeSettings.GossipTimeout);
			var persistentSubscriptionController =
				new PersistentSubscriptionController(httpSendService, _mainQueue, _workersHandler);
			var electController = new ElectController(_mainQueue);

			// HTTP SENDERS
			gossipController.SubscribeSenders(httpPipe);
			electController.SubscribeSenders(httpPipe);

			// EXTERNAL HTTP
			_externalHttpService = new HttpService(ServiceAccessibility.Public, _mainQueue, new TrieUriRouter(),
				_workersHandler, vNodeSettings.LogHttpRequests, vNodeSettings.GossipAdvertiseInfo.AdvertiseExternalIPAs,
				vNodeSettings.GossipAdvertiseInfo.AdvertiseExternalHttpPortAs, vNodeSettings.DisableFirstLevelHttpAuthorization, vNodeSettings.ExtHttpPrefixes);
			_externalHttpService.SetupController(persistentSubscriptionController);
			if (vNodeSettings.AdminOnPublic)
				_externalHttpService.SetupController(adminController);
			_externalHttpService.SetupController(pingController);
			_externalHttpService.SetupController(infoController);
			if (vNodeSettings.StatsOnPublic)
				_externalHttpService.SetupController(statController);
			_externalHttpService.SetupController(atomController);
			if (vNodeSettings.GossipOnPublic)
				_externalHttpService.SetupController(gossipController);
			_externalHttpService.SetupController(histogramController);

			_mainBus.Subscribe<SystemMessage.SystemInit>(_externalHttpService);
			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(_externalHttpService);
			_mainBus.Subscribe<HttpMessage.PurgeTimedOutRequests>(_externalHttpService);
			// INTERNAL HTTP
			if (!isSingleNode) {
				_internalHttpService = new HttpService(ServiceAccessibility.Private, _mainQueue, new TrieUriRouter(),
					_workersHandler, vNodeSettings.LogHttpRequests,
					vNodeSettings.GossipAdvertiseInfo.AdvertiseInternalIPAs,
					vNodeSettings.GossipAdvertiseInfo.AdvertiseInternalHttpPortAs, vNodeSettings.DisableFirstLevelHttpAuthorization, vNodeSettings.IntHttpPrefixes);
				_internalHttpService.SetupController(adminController);
				_internalHttpService.SetupController(pingController);
				_internalHttpService.SetupController(infoController);
				_internalHttpService.SetupController(statController);
				_internalHttpService.SetupController(atomController);
				_internalHttpService.SetupController(gossipController);
				_internalHttpService.SetupController(electController);
				_internalHttpService.SetupController(histogramController);
				_internalHttpService.SetupController(persistentSubscriptionController);
			}

			// Authentication plugin HTTP
			vNodeSettings.AuthenticationProviderFactory.RegisterHttpControllers(_externalHttpService,
				_internalHttpService, httpSendService, _mainQueue, _workersHandler);
			if (_internalHttpService != null) {
				_mainBus.Subscribe<SystemMessage.SystemInit>(_internalHttpService);
				_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(_internalHttpService);
				_mainBus.Subscribe<HttpMessage.PurgeTimedOutRequests>(_internalHttpService);
			}

			SubscribeWorkers(bus => {
				HttpService.CreateAndSubscribePipeline(bus, httpAuthenticationProviders.ToArray());
			});

			// REQUEST FORWARDING
			var forwardingService = new RequestForwardingService(_mainQueue, forwardingProxy, TimeSpan.FromSeconds(1));
			_mainBus.Subscribe<SystemMessage.SystemStart>(forwardingService);
			_mainBus.Subscribe<SystemMessage.RequestForwardingTimerTick>(forwardingService);
			_mainBus.Subscribe<ClientMessage.NotHandled>(forwardingService);
			_mainBus.Subscribe<ClientMessage.WriteEventsCompleted>(forwardingService);
			_mainBus.Subscribe<ClientMessage.TransactionStartCompleted>(forwardingService);
			_mainBus.Subscribe<ClientMessage.TransactionWriteCompleted>(forwardingService);
			_mainBus.Subscribe<ClientMessage.TransactionCommitCompleted>(forwardingService);
			_mainBus.Subscribe<ClientMessage.DeleteStreamCompleted>(forwardingService);

			// REQUEST MANAGEMENT
			var requestManagement = new RequestManagementService(_mainQueue,
				vNodeSettings.PrepareAckCount,
				vNodeSettings.PrepareTimeout,
				vNodeSettings.CommitTimeout,
				vNodeSettings.BetterOrdering);
			_mainBus.Subscribe<SystemMessage.SystemInit>(requestManagement);
			_mainBus.Subscribe<ClientMessage.WriteEvents>(requestManagement);
			_mainBus.Subscribe<ClientMessage.TransactionStart>(requestManagement);
			_mainBus.Subscribe<ClientMessage.TransactionWrite>(requestManagement);
			_mainBus.Subscribe<ClientMessage.TransactionCommit>(requestManagement);
			_mainBus.Subscribe<ClientMessage.DeleteStream>(requestManagement);
			_mainBus.Subscribe<StorageMessage.RequestCompleted>(requestManagement);
			_mainBus.Subscribe<StorageMessage.CheckStreamAccessCompleted>(requestManagement);
			_mainBus.Subscribe<StorageMessage.AlreadyCommitted>(requestManagement);
			_mainBus.Subscribe<StorageMessage.CommitReplicated>(requestManagement);
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
			_mainBus.Subscribe(subscrQueue.WidenFrom<ClientMessage.SubscribeToStreamFiltered, Message>());
			_mainBus.Subscribe(subscrQueue.WidenFrom<ClientMessage.UnsubscribeFromStream, Message>());
			_mainBus.Subscribe(subscrQueue.WidenFrom<SubscriptionMessage.PollStream, Message>());
			_mainBus.Subscribe(subscrQueue.WidenFrom<SubscriptionMessage.CheckPollTimeout, Message>());
			_mainBus.Subscribe(subscrQueue.WidenFrom<StorageMessage.EventCommitted, Message>());

			var subscription = new SubscriptionsService(_mainQueue, subscrQueue, readIndex);
			subscrBus.Subscribe<SystemMessage.SystemStart>(subscription);
			subscrBus.Subscribe<SystemMessage.BecomeShuttingDown>(subscription);
			subscrBus.Subscribe<TcpMessage.ConnectionClosed>(subscription);
			subscrBus.Subscribe<ClientMessage.SubscribeToStream>(subscription);
			subscrBus.Subscribe<ClientMessage.SubscribeToStreamFiltered>(subscription);
			subscrBus.Subscribe<ClientMessage.UnsubscribeFromStream>(subscription);
			subscrBus.Subscribe<SubscriptionMessage.PollStream>(subscription);
			subscrBus.Subscribe<SubscriptionMessage.CheckPollTimeout>(subscription);
			subscrBus.Subscribe<StorageMessage.EventCommitted>(subscription);

			// PERSISTENT SUBSCRIPTIONS
			// IO DISPATCHER
			var ioDispatcher = new IODispatcher(_mainQueue, new PublishEnvelope(_mainQueue));
			_mainBus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(ioDispatcher.BackwardReader);
			_mainBus.Subscribe<ClientMessage.WriteEventsCompleted>(ioDispatcher.Writer);
			_mainBus.Subscribe<ClientMessage.ReadStreamEventsForwardCompleted>(ioDispatcher.ForwardReader);
			_mainBus.Subscribe<ClientMessage.DeleteStreamCompleted>(ioDispatcher.StreamDeleter);
			_mainBus.Subscribe(ioDispatcher);
			var perSubscrBus = new InMemoryBus("PersistentSubscriptionsBus", true, TimeSpan.FromMilliseconds(50));
			var perSubscrQueue = new QueuedHandlerThreadPool(perSubscrBus, "PersistentSubscriptions", false);
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<SystemMessage.StateChangeMessage, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<TcpMessage.ConnectionClosed, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.CreatePersistentSubscription, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.UpdatePersistentSubscription, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.DeletePersistentSubscription, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.ConnectToPersistentSubscription, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.UnsubscribeFromStream, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.PersistentSubscriptionAckEvents, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.PersistentSubscriptionNackEvents, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.ReplayAllParkedMessages, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.ReplayParkedMessage, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.ReadNextNPersistentMessages, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<StorageMessage.EventCommitted, Message>());
			_mainBus.Subscribe(perSubscrQueue
				.WidenFrom<MonitoringMessage.GetAllPersistentSubscriptionStats, Message>());
			_mainBus.Subscribe(
				perSubscrQueue.WidenFrom<MonitoringMessage.GetStreamPersistentSubscriptionStats, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<MonitoringMessage.GetPersistentSubscriptionStats, Message>());
			_mainBus.Subscribe(perSubscrQueue
				.WidenFrom<SubscriptionMessage.PersistentSubscriptionTimerTick, Message>());

			//TODO CC can have multiple threads working on subscription if partition
			var consumerStrategyRegistry =
				new PersistentSubscriptionConsumerStrategyRegistry(_mainQueue, _mainBus,
					vNodeSettings.AdditionalConsumerStrategies);
			var persistentSubscription = new PersistentSubscriptionService(perSubscrQueue, readIndex, ioDispatcher,
				_mainQueue, consumerStrategyRegistry);
			perSubscrBus.Subscribe<SystemMessage.BecomeShuttingDown>(persistentSubscription);
			perSubscrBus.Subscribe<SystemMessage.BecomeMaster>(persistentSubscription);
			perSubscrBus.Subscribe<SystemMessage.StateChangeMessage>(persistentSubscription);
			perSubscrBus.Subscribe<TcpMessage.ConnectionClosed>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.ConnectToPersistentSubscription>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.UnsubscribeFromStream>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.PersistentSubscriptionAckEvents>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.PersistentSubscriptionNackEvents>(persistentSubscription);
			perSubscrBus.Subscribe<StorageMessage.EventCommitted>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.DeletePersistentSubscription>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.CreatePersistentSubscription>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.UpdatePersistentSubscription>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.ReplayAllParkedMessages>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.ReplayParkedMessage>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.ReadNextNPersistentMessages>(persistentSubscription);
			perSubscrBus.Subscribe<MonitoringMessage.GetAllPersistentSubscriptionStats>(persistentSubscription);
			perSubscrBus.Subscribe<MonitoringMessage.GetStreamPersistentSubscriptionStats>(persistentSubscription);
			perSubscrBus.Subscribe<MonitoringMessage.GetPersistentSubscriptionStats>(persistentSubscription);
			perSubscrBus.Subscribe<SubscriptionMessage.PersistentSubscriptionTimerTick>(persistentSubscription);

			// STORAGE SCAVENGER
			var scavengerLogManager = new TFChunkScavengerLogManager(_nodeInfo.ExternalHttp.ToString(),
				TimeSpan.FromDays(vNodeSettings.ScavengeHistoryMaxAge), ioDispatcher);
			var storageScavenger = new StorageScavenger(db,
				tableIndex,
				readIndex,
				scavengerLogManager,
				vNodeSettings.AlwaysKeepScavenged,
				!vNodeSettings.DisableScavengeMerging,
				unsafeIgnoreHardDeletes: vNodeSettings.UnsafeIgnoreHardDeletes);

			// ReSharper disable RedundantTypeArgumentsOfMethod
			_mainBus.Subscribe<ClientMessage.ScavengeDatabase>(storageScavenger);
			_mainBus.Subscribe<ClientMessage.StopDatabaseScavenge>(storageScavenger);
			_mainBus.Subscribe<SystemMessage.StateChangeMessage>(storageScavenger);
			// ReSharper restore RedundantTypeArgumentsOfMethod


			// TIMER
			_timeProvider = new RealTimeProvider();
			var threadBasedScheduler = new ThreadBasedScheduler(_timeProvider);
			AddTask(threadBasedScheduler.Task);
			_timerService = new TimerService(threadBasedScheduler);
			_mainBus.Subscribe<SystemMessage.BecomeShutdown>(_timerService);
			_mainBus.Subscribe<TimerMessage.Schedule>(_timerService);

			var gossipInfo = new VNodeInfo(_nodeInfo.InstanceId, _nodeInfo.DebugIndex,
				vNodeSettings.GossipAdvertiseInfo.InternalTcp,
				vNodeSettings.GossipAdvertiseInfo.InternalSecureTcp,
				vNodeSettings.GossipAdvertiseInfo.ExternalTcp,
				vNodeSettings.GossipAdvertiseInfo.ExternalSecureTcp,
				vNodeSettings.GossipAdvertiseInfo.InternalHttp,
				vNodeSettings.GossipAdvertiseInfo.ExternalHttp,
				vNodeSettings.ReadOnlyReplica);
			if (!isSingleNode) {
				// MASTER REPLICATION
				var masterReplicationService = new MasterReplicationService(_mainQueue, gossipInfo.InstanceId, db,
					_workersHandler,
					epochManager, vNodeSettings.ClusterNodeCount);
				AddTask(masterReplicationService.Task);
				_mainBus.Subscribe<SystemMessage.SystemStart>(masterReplicationService);
				_mainBus.Subscribe<SystemMessage.StateChangeMessage>(masterReplicationService);
				_mainBus.Subscribe<ReplicationMessage.ReplicaSubscriptionRequest>(masterReplicationService);
				_mainBus.Subscribe<ReplicationMessage.ReplicaLogPositionAck>(masterReplicationService);
				monitoringInnerBus.Subscribe<ReplicationMessage.GetReplicationStats>(masterReplicationService);

				// REPLICA REPLICATION
				var replicaService = new ReplicaService(_mainQueue, db, epochManager, _workersHandler,
					_internalAuthenticationProvider,
					gossipInfo, vNodeSettings.UseSsl, vNodeSettings.SslTargetHost, vNodeSettings.SslValidateServer,
					vNodeSettings.IntTcpHeartbeatTimeout, vNodeSettings.ExtTcpHeartbeatInterval);
				_mainBus.Subscribe<SystemMessage.StateChangeMessage>(replicaService);
				_mainBus.Subscribe<ReplicationMessage.ReconnectToMaster>(replicaService);
				_mainBus.Subscribe<ReplicationMessage.SubscribeToMaster>(replicaService);
				_mainBus.Subscribe<ReplicationMessage.AckLogPosition>(replicaService);
				_mainBus.Subscribe<StorageMessage.PrepareAck>(replicaService);
				_mainBus.Subscribe<StorageMessage.CommitAck>(replicaService);
				_mainBus.Subscribe<ClientMessage.TcpForwardMessage>(replicaService);
			}

			// ELECTIONS

			var electionsService = new ElectionsService(_mainQueue, gossipInfo, vNodeSettings.ClusterNodeCount,
				db.Config.WriterCheckpoint, db.Config.ChaserCheckpoint,
				epochManager, () => readIndex.LastCommitPosition, vNodeSettings.NodePriority);
			electionsService.SubscribeMessages(_mainBus);
			if (!isSingleNode || vNodeSettings.GossipOnSingleNode) {
				// GOSSIP

				var gossip = new NodeGossipService(_mainQueue, gossipSeedSource, gossipInfo, db.Config.WriterCheckpoint,
					db.Config.ChaserCheckpoint, epochManager, () => readIndex.LastCommitPosition,
					vNodeSettings.NodePriority, vNodeSettings.GossipInterval,
					vNodeSettings.GossipAllowedTimeDifference);
				_mainBus.Subscribe<SystemMessage.SystemInit>(gossip);
				_mainBus.Subscribe<GossipMessage.RetrieveGossipSeedSources>(gossip);
				_mainBus.Subscribe<GossipMessage.GotGossipSeedSources>(gossip);
				_mainBus.Subscribe<GossipMessage.Gossip>(gossip);
				_mainBus.Subscribe<GossipMessage.GossipReceived>(gossip);
				_mainBus.Subscribe<SystemMessage.StateChangeMessage>(gossip);
				_mainBus.Subscribe<GossipMessage.GossipSendFailed>(gossip);
				_mainBus.Subscribe<SystemMessage.VNodeConnectionEstablished>(gossip);
				_mainBus.Subscribe<SystemMessage.VNodeConnectionLost>(gossip);
			}

			AddTasks(_workersHandler.Start());
			AddTask(_mainQueue.Start());
			AddTask(monitoringQueue.Start());
			AddTask(subscrQueue.Start());
			AddTask(perSubscrQueue.Start());

			if (subsystems != null) {
				foreach (var subsystem in subsystems) {
					var http = isSingleNode
						? new[] {_externalHttpService}
						: new[] {_internalHttpService, _externalHttpService};
					subsystem.Register(new StandardComponents(db, _mainQueue, _mainBus, _timerService, _timeProvider,
						httpSendService, http, _workersHandler));
				}
			}
		}

		private void SubscribeWorkers(Action<InMemoryBus> setup) {
			foreach (var workerBus in _workerBuses) {
				setup(workerBus);
			}
		}

		public void Start() {
			_mainQueue.Publish(new SystemMessage.SystemInit());
		}

		public void StopNonblocking(bool exitProcess, bool shutdownHttp) {
			_mainQueue.Publish(new ClientMessage.RequestShutdown(exitProcess, shutdownHttp));

			if (_subsystems == null) return;
			foreach (var subsystem in _subsystems)
				subsystem.Stop();
		}

		public bool Stop() {
			return Stop(TimeSpan.FromSeconds(15), false, true);
		}

		public bool Stop(TimeSpan timeout, bool exitProcess, bool shutdownHttp) {
			StopNonblocking(exitProcess, shutdownHttp);
			return _shutdownEvent.WaitOne(timeout);
		}

		public void Handle(SystemMessage.StateChangeMessage message) {
			OnNodeStatusChanged(new VNodeStatusChangeArgs(message.State));
		}

		public void Handle(SystemMessage.BecomeShutdown message) {
			_shutdownEvent.Set();
		}

		public void AddTasks(IEnumerable<Task> tasks) {
#if DEBUG
			foreach (var task in tasks) {
				AddTask(task);
			}
#endif
		}

		public void AddTask(Task task) {
#if DEBUG
			lock (_taskAddLock) {
				_tasks.Add(task);

				//keep reference to old trigger task
				var oldTrigger = _taskAddedTrigger;

				//create and add new trigger task to list
				_taskAddedTrigger = new TaskCompletionSource<bool>();
				_tasks.Add(_taskAddedTrigger.Task);

				//remove old trigger task from list
				_tasks.Remove(oldTrigger.Task);

				//trigger old trigger task
				oldTrigger.SetResult(true);
			}
#endif
		}

		public Task<ClusterVNode> StartAndWaitUntilReady() {
			var tcs = new TaskCompletionSource<ClusterVNode>(TaskCreationOptions.RunContinuationsAsynchronously);

			_mainBus.Subscribe(new AdHocHandler<SystemMessage.SystemReady>(
				_ => tcs.TrySetResult(this)));

			Start();

			return tcs.Task;
		}

		public override string ToString() {
			return string.Format("[{0:B}, {1}, {2}, {3}, {4}]", _nodeInfo.InstanceId,
				_nodeInfo.InternalTcp, _nodeInfo.ExternalTcp, _nodeInfo.InternalHttp, _nodeInfo.ExternalHttp);
		}
	}
}
