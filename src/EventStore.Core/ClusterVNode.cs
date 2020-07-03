using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
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
using EventStore.Core.Services.Histograms;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;
using System.Threading.Tasks;
using EventStore.Core.Authorization;
using EventStore.Core.Cluster;
using EventStore.Plugins.Authentication;
using EventStore.Plugins.Authorization;
using Microsoft.AspNetCore.Hosting;
using ILogger = Serilog.ILogger;
using MidFunc = System.Func<
	Microsoft.AspNetCore.Http.HttpContext,
	System.Func<System.Threading.Tasks.Task>,
	System.Threading.Tasks.Task
>;

namespace EventStore.Core {
	public class ClusterVNode :
		IHandle<SystemMessage.StateChangeMessage>,
		IHandle<SystemMessage.BecomeShuttingDown>,
		IHandle<SystemMessage.BecomeShutdown>,
		IHandle<SystemMessage.SystemStart> {
		private static readonly ILogger Log = Serilog.Log.ForContext<ClusterVNode>();

		public IQueuedHandler MainQueue {
			get { return _mainQueue; }
		}

		public ISubscriber MainBus {
			get { return _mainBus; }
		}

		public IHttpService HttpService {
			get { return _httpService; }
		}

		public IReadIndex ReadIndex => _readIndex;

		public TimerService TimerService {
			get { return _timerService; }
		}

		public IPublisher NetworkSendService {
			get { return _workersHandler; }
		}

		public QueueStatsManager QueueStatsManager => _queueStatsManager;

		public IStartup Startup => _startup;
		
		public IAuthenticationProvider AuthenticationProvider {
			get { return _authenticationProvider; }
		}

		public AuthorizationGateway AuthorizationGateway { get; }

		internal MultiQueuedHandler WorkersHandler {
			get { return _workersHandler; }
		}

		public IEnumerable<ISubsystem> Subsystems => _subsystems;

		private readonly VNodeInfo _nodeInfo;
		private readonly IQueuedHandler _mainQueue;
		private readonly ISubscriber _mainBus;

		private readonly ClusterVNodeController _controller;
		private readonly TimerService _timerService;
		private readonly KestrelHttpService _httpService;
		private readonly ITimeProvider _timeProvider;
		private readonly ISubsystem[] _subsystems;
		private readonly TaskCompletionSource<bool> _shutdownSource = new TaskCompletionSource<bool>();
		private readonly IAuthenticationProvider _authenticationProvider;
		private readonly IAuthorizationProvider _authorizationProvider;
		private readonly IReadIndex _readIndex;

		private readonly InMemoryBus[] _workerBuses;
		private readonly MultiQueuedHandler _workersHandler;
		public event EventHandler<VNodeStatusChangeArgs> NodeStatusChanged;
		private readonly List<Task> _tasks = new List<Task>();
		private readonly QueueStatsManager _queueStatsManager;
		private readonly X509Certificate2 _certificate;
		private readonly bool _disableHttps;
		private readonly Func<X509Certificate2> _certificateSelector;
		private readonly Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> _internalServerCertificateValidator;
		private readonly Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> _internalClientCertificateValidator;
		private readonly Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> _externalClientCertificateValidator;
		private readonly Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> _externalServerCertificateValidator;

		private readonly ClusterVNodeSettings _vNodeSettings;
		private readonly ClusterVNodeStartup _startup;
		private readonly EventStoreClusterClientCache _eventStoreClusterClientCache;

		private int _stopCalled;

		public IEnumerable<Task> Tasks {
			get { return _tasks; }
		}

		public X509Certificate2 Certificate => _certificate;
		public Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> InternalClientCertificateValidator => _internalClientCertificateValidator;
		public bool DisableHttps => _disableHttps;

#if DEBUG
		public TaskCompletionSource<bool> _taskAddedTrigger = new TaskCompletionSource<bool>();
		public object _taskAddLock = new object();
#endif

		protected virtual void OnNodeStatusChanged(VNodeStatusChangeArgs e) {
			EventHandler<VNodeStatusChangeArgs> handler = NodeStatusChanged;
			if (handler != null)
				handler(this, e);
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
			_vNodeSettings = vNodeSettings;
			_nodeInfo = vNodeSettings.NodeInfo;
			_certificate = vNodeSettings.Certificate;
			_disableHttps = vNodeSettings.DisableHttps;
			_mainBus = new InMemoryBus("MainBus");
			_queueStatsManager = new QueueStatsManager();

			_certificateSelector = () => _certificate;
			_internalServerCertificateValidator = (cert, chain, errors) =>  ValidateServerCertificateWithTrustedRootCerts(cert, chain, errors, _vNodeSettings.TrustedRootCerts);
			_internalClientCertificateValidator = (cert, chain, errors) =>  ValidateClientCertificateWithTrustedRootCerts(cert, chain, errors, _vNodeSettings.TrustedRootCerts);
			_externalClientCertificateValidator = delegate { return (true, null); };
			_externalServerCertificateValidator = (cert, chain, errors) => ValidateServerCertificateWithTrustedRootCerts(cert, chain, errors, _vNodeSettings.TrustedRootCerts);

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
					_queueStatsManager,
					groupName: "Workers",
					watchSlowMsg: true,
					slowMsgThreshold: TimeSpan.FromMilliseconds(200)));

			_subsystems = subsystems;

			_controller = new ClusterVNodeController((IPublisher)_mainBus, _nodeInfo, db, vNodeSettings, this,
				forwardingProxy, _subsystems);
			_mainQueue = QueuedHandler.CreateQueuedHandler(_controller, "MainQueue", _queueStatsManager);

			_controller.SetMainQueue(_mainQueue);

			_eventStoreClusterClientCache = new EventStoreClusterClientCache(_mainQueue,
				(endpoint, publisher) =>
					new EventStoreClusterClient(
						new UriBuilder(!_vNodeSettings.DisableHttps ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
							endpoint.GetHost(), endpoint.GetPort()).Uri, publisher, _internalServerCertificateValidator, _certificate));

			_mainBus.Subscribe<ClusterClientMessage.CleanCache>(_eventStoreClusterClientCache);
			_mainBus.Subscribe<SystemMessage.SystemInit>(_eventStoreClusterClientCache);

			//SELF
			_mainBus.Subscribe<SystemMessage.StateChangeMessage>(this);
			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(this);
			_mainBus.Subscribe<SystemMessage.BecomeShutdown>(this);
			_mainBus.Subscribe<SystemMessage.SystemStart>(this);
			// MONITORING
			var monitoringInnerBus = new InMemoryBus("MonitoringInnerBus", watchSlowMsg: false);
			var monitoringRequestBus = new InMemoryBus("MonitoringRequestBus", watchSlowMsg: false);
			var monitoringQueue = new QueuedHandlerThreadPool(monitoringInnerBus, "MonitoringQueue", _queueStatsManager, true,
				TimeSpan.FromMilliseconds(800));
			var monitoring = new MonitoringService(monitoringQueue,
				monitoringRequestBus,
				_mainQueue,
				db.Config.WriterCheckpoint,
				db.Config.Path,
				vNodeSettings.StatsPeriod,
				_nodeInfo.HttpEndPoint,
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
				Log.Information(
					"Truncate checkpoint is present. Truncate: {truncatePosition} (0x{truncatePosition:X}), Writer: {writerCheckpoint} (0x{writerCheckpoint:X}), Chaser: {chaserCheckpoint} (0x{chaserCheckpoint:X}), Epoch: {epochCheckpoint} (0x{epochCheckpoint:X})",
					truncPos, truncPos, writerCheckpoint, writerCheckpoint, chaserCheckpoint, chaserCheckpoint,
					epochCheckpoint, epochCheckpoint);
				var truncator = new TFChunkDbTruncator(db.Config);
				truncator.TruncateDb(truncPos);
			}

			// STORAGE SUBSYSTEM
			db.Open(vNodeSettings.VerifyDbHash, threads: vNodeSettings.InitializationThreads);
			var indexPath = vNodeSettings.Index ?? Path.Combine(db.Config.Path, "index");
			var readerPool = new ObjectPool<ITransactionFileReader>(
				"ReadIndex readers pool", ESConsts.PTableInitialReaderCount, vNodeSettings.PTableMaxReaderCount,
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
				maxAutoMergeIndexLevel: vNodeSettings.MaxAutoMergeIndexLevel,
				pTableMaxReaderCount: vNodeSettings.PTableMaxReaderCount);
			var readIndex = new ReadIndex(_mainQueue,
				readerPool,
				tableIndex,
				ESConsts.StreamInfoCacheCapacity,
				ESConsts.PerformAdditionlCommitChecks,
				ESConsts.MetaStreamMaxCount,
				vNodeSettings.HashCollisionReadLimit,
				vNodeSettings.SkipIndexScanOnReads,
				db.Config.ReplicationCheckpoint,
				db.Config.IndexCheckpoint);
			_readIndex = readIndex;
			var writer = new TFChunkWriter(db);
			var epochManager = new EpochManager(_mainQueue,
				ESConsts.CachedEpochCount,
				db.Config.EpochCheckpoint,
				writer,
				initialReaderCount: 1,
				maxReaderCount: 5,
				readerFactory: () => new TFChunkReader(db, db.Config.WriterCheckpoint,
					optimizeReadSideCache: db.Config.OptimizeReadSideCache),
				_nodeInfo.InstanceId);
			epochManager.Init();

			var storageWriter = new ClusterStorageWriterService(_mainQueue, _mainBus, vNodeSettings.MinFlushDelay,
				db, writer, readIndex.IndexWriter, epochManager, _queueStatsManager,
				() => readIndex.LastIndexedPosition); // subscribes internally
			AddTasks(storageWriter.Tasks);

			monitoringRequestBus.Subscribe<MonitoringMessage.InternalStatsRequest>(storageWriter);

			var storageReader = new StorageReaderService(_mainQueue, _mainBus, readIndex,
				vNodeSettings.ReaderThreadsCount, db.Config.WriterCheckpoint, _queueStatsManager);
			_mainBus.Subscribe<SystemMessage.SystemInit>(storageReader);
			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(storageReader);
			_mainBus.Subscribe<SystemMessage.BecomeShutdown>(storageReader);
			monitoringRequestBus.Subscribe<MonitoringMessage.InternalStatsRequest>(storageReader);


			//REPLICATION TRACKING
			var replicationTracker =
				new ReplicationTrackingService(_mainQueue, vNodeSettings.ClusterNodeCount, db.Config.ReplicationCheckpoint, db.Config.WriterCheckpoint);
			AddTask(replicationTracker.Task);
			_mainBus.Subscribe<SystemMessage.SystemInit>(replicationTracker);
			_mainBus.Subscribe<SystemMessage.StateChangeMessage>(replicationTracker);
			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(replicationTracker);
			_mainBus.Subscribe<ReplicationTrackingMessage.ReplicaWriteAck>(replicationTracker);
			_mainBus.Subscribe<ReplicationTrackingMessage.WriterCheckpointFlushed>(replicationTracker);
			_mainBus.Subscribe<ReplicationTrackingMessage.LeaderReplicatedTo>(replicationTracker);
			_mainBus.Subscribe<SystemMessage.VNodeConnectionLost>(replicationTracker);

			var indexCommitterService = new IndexCommitterService(readIndex.IndexCommitter, _mainQueue,
				db.Config.WriterCheckpoint, db.Config.ReplicationCheckpoint, vNodeSettings.CommitAckCount, tableIndex, _queueStatsManager);
			AddTask(indexCommitterService.Task);

			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(indexCommitterService);
			_mainBus.Subscribe<ReplicationTrackingMessage.ReplicatedTo>(indexCommitterService);
			_mainBus.Subscribe<StorageMessage.CommitAck>(indexCommitterService);
			_mainBus.Subscribe<ClientMessage.MergeIndexes>(indexCommitterService);

			var chaser = new TFChunkChaser(db, db.Config.WriterCheckpoint, db.Config.ChaserCheckpoint,
				db.Config.OptimizeReadSideCache);
			var storageChaser = new StorageChaser(_mainQueue, db.Config.WriterCheckpoint, chaser, indexCommitterService,
				epochManager, _queueStatsManager);
			AddTask(storageChaser.Task);

#if DEBUG
			_queueStatsManager.InitializeCheckpoints(db.Config.WriterCheckpoint, db.Config.ChaserCheckpoint);
#endif
			_mainBus.Subscribe<SystemMessage.SystemInit>(storageChaser);
			_mainBus.Subscribe<SystemMessage.SystemStart>(storageChaser);
			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(storageChaser);

			var httpPipe = new HttpMessagePipe();
			var httpSendService = new HttpSendService(httpPipe, true, _externalServerCertificateValidator);
			_mainBus.Subscribe<SystemMessage.StateChangeMessage>(httpSendService);
			SubscribeWorkers(bus => bus.Subscribe<HttpMessage.HttpSend>(httpSendService));

			var grpcSendService = new GrpcSendService(_eventStoreClusterClientCache);
			_mainBus.Subscribe(new WideningHandler<GrpcMessage.SendOverGrpc, Message>(_workersHandler));
			SubscribeWorkers(bus => {
				bus.Subscribe<GrpcMessage.SendOverGrpc>(grpcSendService);
			});

			_httpService = new KestrelHttpService(ServiceAccessibility.Public, _mainQueue, new TrieUriRouter(),
				_workersHandler, vNodeSettings.LogHttpRequests,
				vNodeSettings.GossipAdvertiseInfo.AdvertiseExternalHostAs,
				vNodeSettings.GossipAdvertiseInfo.AdvertiseHttpPortAs,
				vNodeSettings.DisableFirstLevelHttpAuthorization,
				vNodeSettings.NodeInfo.HttpEndPoint);

			var components = new AuthenticationProviderFactoryComponents {
				MainBus = _mainBus,
				MainQueue = _mainQueue,
				WorkerBuses = _workerBuses,
				WorkersQueue = _workersHandler,
				HttpSendService = httpSendService,
				HttpService = _httpService,
			};

			// AUTHENTICATION INFRASTRUCTURE - delegate to plugins
			_authenticationProvider =
				vNodeSettings.AuthenticationProviderFactory.GetFactory(components).Build(
					vNodeSettings.LogFailedAuthenticationAttempts, Log);
			Ensure.NotNull(_authenticationProvider, nameof(_authenticationProvider));

			_authorizationProvider = vNodeSettings.AuthorizationProviderFactory
				.GetFactory(new AuthorizationProviderFactoryComponents {
					MainQueue = _mainQueue
				}).Build();
			Ensure.NotNull(_authorizationProvider, "authorizationProvider");

			AuthorizationGateway = new AuthorizationGateway(_authorizationProvider);
			{
				// EXTERNAL TCP
				if (_nodeInfo.ExternalTcp != null && vNodeSettings.EnableExternalTCP) {
					var extTcpService = new TcpService(_mainQueue, _nodeInfo.ExternalTcp, _workersHandler,
						TcpServiceType.External, TcpSecurityType.Normal,
						new ClientTcpDispatcher(vNodeSettings.WriteTimeout),
						vNodeSettings.ExtTcpHeartbeatInterval, vNodeSettings.ExtTcpHeartbeatTimeout,
						_authenticationProvider, AuthorizationGateway, null, null,
						vNodeSettings.ConnectionPendingSendBytesThreshold,
					vNodeSettings.ConnectionQueueSizeThreshold);
					_mainBus.Subscribe<SystemMessage.SystemInit>(extTcpService);
					_mainBus.Subscribe<SystemMessage.SystemStart>(extTcpService);
					_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(extTcpService);
				}
				// EXTERNAL SECURE TCP
				if (_nodeInfo.ExternalSecureTcp != null && vNodeSettings.EnableExternalTCP) {
					var extSecTcpService = new TcpService(_mainQueue, _nodeInfo.ExternalSecureTcp, _workersHandler,
						TcpServiceType.External, TcpSecurityType.Secure,
						new ClientTcpDispatcher(vNodeSettings.WriteTimeout),
						vNodeSettings.ExtTcpHeartbeatInterval, vNodeSettings.ExtTcpHeartbeatTimeout,
						_authenticationProvider, AuthorizationGateway, _certificateSelector, _externalClientCertificateValidator,
						vNodeSettings.ConnectionPendingSendBytesThreshold, vNodeSettings.ConnectionQueueSizeThreshold);
					_mainBus.Subscribe<SystemMessage.SystemInit>(extSecTcpService);
					_mainBus.Subscribe<SystemMessage.SystemStart>(extSecTcpService);
					_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(extSecTcpService);
				}

				if (!isSingleNode) {
					// INTERNAL TCP
					if (_nodeInfo.InternalTcp != null) {
						var intTcpService = new TcpService(_mainQueue, _nodeInfo.InternalTcp, _workersHandler,
							TcpServiceType.Internal, TcpSecurityType.Normal,
							new InternalTcpDispatcher(vNodeSettings.WriteTimeout),
							vNodeSettings.IntTcpHeartbeatInterval, vNodeSettings.IntTcpHeartbeatTimeout,
							_authenticationProvider, AuthorizationGateway, null, null, ESConsts.UnrestrictedPendingSendBytes,
						ESConsts.MaxConnectionQueueSize);
						_mainBus.Subscribe<SystemMessage.SystemInit>(intTcpService);
						_mainBus.Subscribe<SystemMessage.SystemStart>(intTcpService);
						_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(intTcpService);
					}
					// INTERNAL SECURE TCP
					if (_nodeInfo.InternalSecureTcp != null) {
						var intSecTcpService = new TcpService(_mainQueue, _nodeInfo.InternalSecureTcp, _workersHandler,
							TcpServiceType.Internal, TcpSecurityType.Secure,
							new InternalTcpDispatcher(vNodeSettings.WriteTimeout),
							vNodeSettings.IntTcpHeartbeatInterval, vNodeSettings.IntTcpHeartbeatTimeout,
							_authenticationProvider, AuthorizationGateway, _certificateSelector, _internalClientCertificateValidator,
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

			var httpAuthenticationProviders = new List<IHttpAuthenticationProvider> {
				new BasicHttpAuthenticationProvider(_authenticationProvider),
				new BearerHttpAuthenticationProvider(_authenticationProvider)
			};

			if (!_disableHttps) {
				httpAuthenticationProviders.Add(
					new ClientCertificateAuthenticationProvider(_vNodeSettings.CertificateReservedNodeCommonName));
			} else {
				httpAuthenticationProviders.Add(new GossipAndElectionsAuthenticationProvider());
			}

			if (vNodeSettings.EnableTrustedAuth)
				httpAuthenticationProviders.Add(new TrustedHttpAuthenticationProvider());
			httpAuthenticationProviders.Add(new AnonymousHttpAuthenticationProvider());

			_mainBus.Subscribe<SystemMessage.StateChangeMessage>(infoController);

			var adminController = new AdminController(_mainQueue, _workersHandler);
			var pingController = new PingController();
			var histogramController = new HistogramController();
			var statController = new StatController(monitoringQueue, _workersHandler);
			var atomController = new AtomController(_mainQueue, _workersHandler,
				vNodeSettings.DisableHTTPCaching, vNodeSettings.WriteTimeout);
			var gossipController = new GossipController(_mainQueue, _workersHandler,
				_internalServerCertificateValidator, _certificate);
			var persistentSubscriptionController =
				new PersistentSubscriptionController(httpSendService, _mainQueue, _workersHandler);

			_httpService.SetupController(persistentSubscriptionController);
			if (vNodeSettings.AdminOnPublic)
				_httpService.SetupController(adminController);
			_httpService.SetupController(pingController);
			_httpService.SetupController(infoController);
			if (vNodeSettings.StatsOnPublic)
				_httpService.SetupController(statController);
			if (vNodeSettings.EnableAtomPubOverHTTP)
				_httpService.SetupController(atomController);
			if (vNodeSettings.GossipOnPublic)
				_httpService.SetupController(gossipController);
			_httpService.SetupController(histogramController);

			_mainBus.Subscribe<SystemMessage.SystemInit>(_httpService);
			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(_httpService);

			SubscribeWorkers(KestrelHttpService.CreateAndSubscribePipeline);

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
			var requestManagement = new RequestManagementService(
				_mainQueue,
				vNodeSettings.PrepareTimeout,
				vNodeSettings.CommitTimeout);

			_mainBus.Subscribe<SystemMessage.SystemInit>(requestManagement);
			_mainBus.Subscribe<SystemMessage.StateChangeMessage>(requestManagement);

			_mainBus.Subscribe<ClientMessage.WriteEvents>(requestManagement);
			_mainBus.Subscribe<ClientMessage.TransactionStart>(requestManagement);
			_mainBus.Subscribe<ClientMessage.TransactionWrite>(requestManagement);
			_mainBus.Subscribe<ClientMessage.TransactionCommit>(requestManagement);
			_mainBus.Subscribe<ClientMessage.DeleteStream>(requestManagement);

			_mainBus.Subscribe<StorageMessage.AlreadyCommitted>(requestManagement);

			_mainBus.Subscribe<StorageMessage.PrepareAck>(requestManagement);
			_mainBus.Subscribe<ReplicationTrackingMessage.ReplicatedTo>(requestManagement);
			_mainBus.Subscribe<ReplicationTrackingMessage.IndexedTo>(requestManagement);
			_mainBus.Subscribe<StorageMessage.RequestCompleted>(requestManagement);
			_mainBus.Subscribe<StorageMessage.CommitIndexed>(requestManagement);

			_mainBus.Subscribe<StorageMessage.WrongExpectedVersion>(requestManagement);
			_mainBus.Subscribe<StorageMessage.InvalidTransaction>(requestManagement);
			_mainBus.Subscribe<StorageMessage.StreamDeleted>(requestManagement);

			_mainBus.Subscribe<StorageMessage.RequestManagerTimerTick>(requestManagement);

			// SUBSCRIPTIONS
			var subscrBus = new InMemoryBus("SubscriptionsBus", true, TimeSpan.FromMilliseconds(50));
			var subscrQueue = new QueuedHandlerThreadPool(subscrBus, "Subscriptions", _queueStatsManager, false);
			_mainBus.Subscribe(subscrQueue.WidenFrom<SystemMessage.SystemStart, Message>());
			_mainBus.Subscribe(subscrQueue.WidenFrom<SystemMessage.BecomeShuttingDown, Message>());
			_mainBus.Subscribe(subscrQueue.WidenFrom<TcpMessage.ConnectionClosed, Message>());
			_mainBus.Subscribe(subscrQueue.WidenFrom<ClientMessage.SubscribeToStream, Message>());
			_mainBus.Subscribe(subscrQueue.WidenFrom<ClientMessage.FilteredSubscribeToStream, Message>());
			_mainBus.Subscribe(subscrQueue.WidenFrom<ClientMessage.UnsubscribeFromStream, Message>());
			_mainBus.Subscribe(subscrQueue.WidenFrom<SubscriptionMessage.PollStream, Message>());
			_mainBus.Subscribe(subscrQueue.WidenFrom<SubscriptionMessage.CheckPollTimeout, Message>());
			_mainBus.Subscribe(subscrQueue.WidenFrom<StorageMessage.EventCommitted, Message>());

			var subscription = new SubscriptionsService(_mainQueue, subscrQueue, readIndex);
			subscrBus.Subscribe<SystemMessage.SystemStart>(subscription);
			subscrBus.Subscribe<SystemMessage.BecomeShuttingDown>(subscription);
			subscrBus.Subscribe<TcpMessage.ConnectionClosed>(subscription);
			subscrBus.Subscribe<ClientMessage.SubscribeToStream>(subscription);
			subscrBus.Subscribe<ClientMessage.FilteredSubscribeToStream>(subscription);
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
			var perSubscrQueue = new QueuedHandlerThreadPool(perSubscrBus, "PersistentSubscriptions", _queueStatsManager, false);
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<SystemMessage.StateChangeMessage, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<TcpMessage.ConnectionClosed, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.CreatePersistentSubscription, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.UpdatePersistentSubscription, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.DeletePersistentSubscription, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.ConnectToPersistentSubscription, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.UnsubscribeFromStream, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.PersistentSubscriptionAckEvents, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.PersistentSubscriptionNackEvents, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.ReplayParkedMessages, Message>());
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
			perSubscrBus.Subscribe<SystemMessage.BecomeLeader>(persistentSubscription);
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
			perSubscrBus.Subscribe<ClientMessage.ReplayParkedMessages>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.ReplayParkedMessage>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.ReadNextNPersistentMessages>(persistentSubscription);
			perSubscrBus.Subscribe<MonitoringMessage.GetAllPersistentSubscriptionStats>(persistentSubscription);
			perSubscrBus.Subscribe<MonitoringMessage.GetStreamPersistentSubscriptionStats>(persistentSubscription);
			perSubscrBus.Subscribe<MonitoringMessage.GetPersistentSubscriptionStats>(persistentSubscription);
			perSubscrBus.Subscribe<SubscriptionMessage.PersistentSubscriptionTimerTick>(persistentSubscription);

			// STORAGE SCAVENGER
			var scavengerLogManager = new TFChunkScavengerLogManager(_nodeInfo.HttpEndPoint.ToString(),
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
			var threadBasedScheduler = new ThreadBasedScheduler(_timeProvider, _queueStatsManager);
			AddTask(threadBasedScheduler.Task);
			_timerService = new TimerService(threadBasedScheduler);
			_mainBus.Subscribe<SystemMessage.BecomeShutdown>(_timerService);
			_mainBus.Subscribe<TimerMessage.Schedule>(_timerService);

			var memberInfo = MemberInfo.Initial(_nodeInfo.InstanceId, _timeProvider.UtcNow, VNodeState.Unknown, true,
				vNodeSettings.GossipAdvertiseInfo.InternalTcp,
				vNodeSettings.GossipAdvertiseInfo.InternalSecureTcp,
				vNodeSettings.GossipAdvertiseInfo.ExternalTcp,
				vNodeSettings.GossipAdvertiseInfo.ExternalSecureTcp,
				vNodeSettings.GossipAdvertiseInfo.HttpEndPoint,
				vNodeSettings.NodePriority, vNodeSettings.ReadOnlyReplica);
			
			if (!isSingleNode) {
				// LEADER REPLICATION
				var leaderReplicationService = new LeaderReplicationService(_mainQueue, _nodeInfo.InstanceId, db,
					_workersHandler,
					epochManager, vNodeSettings.ClusterNodeCount,
					vNodeSettings.UnsafeAllowSurplusNodes,
					_queueStatsManager);
				AddTask(leaderReplicationService.Task);
				_mainBus.Subscribe<SystemMessage.SystemStart>(leaderReplicationService);
				_mainBus.Subscribe<SystemMessage.StateChangeMessage>(leaderReplicationService);
				_mainBus.Subscribe<ReplicationMessage.ReplicaSubscriptionRequest>(leaderReplicationService);
				_mainBus.Subscribe<ReplicationMessage.ReplicaLogPositionAck>(leaderReplicationService);
				_mainBus.Subscribe<ReplicationTrackingMessage.ReplicatedTo>(leaderReplicationService);
				monitoringInnerBus.Subscribe<ReplicationMessage.GetReplicationStats>(leaderReplicationService);

				// REPLICA REPLICATION
				var replicaService = new ReplicaService(_mainQueue, db, epochManager, _workersHandler,
					_authenticationProvider, AuthorizationGateway,
					_vNodeSettings.GossipAdvertiseInfo.InternalTcp ?? _vNodeSettings.GossipAdvertiseInfo.InternalSecureTcp,
					_vNodeSettings.ReadOnlyReplica,
					!vNodeSettings.DisableInternalTcpTls, _internalServerCertificateValidator,
					_certificateSelector,
					vNodeSettings.IntTcpHeartbeatTimeout, vNodeSettings.ExtTcpHeartbeatInterval,
					vNodeSettings.WriteTimeout);
				_mainBus.Subscribe<SystemMessage.StateChangeMessage>(replicaService);
				_mainBus.Subscribe<ReplicationMessage.ReconnectToLeader>(replicaService);
				_mainBus.Subscribe<ReplicationMessage.SubscribeToLeader>(replicaService);
				_mainBus.Subscribe<ReplicationMessage.AckLogPosition>(replicaService);
				_mainBus.Subscribe<ClientMessage.TcpForwardMessage>(replicaService);
			}

			// ELECTIONS
			if (!vNodeSettings.NodeInfo.IsReadOnlyReplica) {
				var electionsService = new ElectionsService(_mainQueue, memberInfo, vNodeSettings.ClusterNodeCount,
					db.Config.WriterCheckpoint, db.Config.ChaserCheckpoint,
					epochManager, () => readIndex.LastIndexedPosition, vNodeSettings.NodePriority, _timeProvider);
				electionsService.SubscribeMessages(_mainBus);
			}

			if (!isSingleNode || vNodeSettings.GossipOnSingleNode) {
				// GOSSIP

				var gossip = new NodeGossipService(_mainQueue, gossipSeedSource, memberInfo, db.Config.WriterCheckpoint,
					db.Config.ChaserCheckpoint, epochManager, () => readIndex.LastIndexedPosition,
					vNodeSettings.NodePriority, vNodeSettings.GossipInterval, vNodeSettings.GossipAllowedTimeDifference,
					vNodeSettings.GossipTimeout,
					vNodeSettings.DeadMemberRemovalPeriod,
					_timeProvider);
				_mainBus.Subscribe<SystemMessage.SystemInit>(gossip);
				_mainBus.Subscribe<GossipMessage.RetrieveGossipSeedSources>(gossip);
				_mainBus.Subscribe<GossipMessage.GotGossipSeedSources>(gossip);
				_mainBus.Subscribe<GossipMessage.Gossip>(gossip);
				_mainBus.Subscribe<GossipMessage.GossipReceived>(gossip);
				_mainBus.Subscribe<GossipMessage.ReadGossip>(gossip);
				_mainBus.Subscribe<SystemMessage.StateChangeMessage>(gossip);
				_mainBus.Subscribe<GossipMessage.GossipSendFailed>(gossip);
				_mainBus.Subscribe<GossipMessage.UpdateNodePriority>(gossip);
				_mainBus.Subscribe<SystemMessage.VNodeConnectionEstablished>(gossip);
				_mainBus.Subscribe<SystemMessage.VNodeConnectionLost>(gossip);
				_mainBus.Subscribe<GossipMessage.GetGossipFailed>(gossip);
				_mainBus.Subscribe<GossipMessage.GetGossipReceived>(gossip);
				_mainBus.Subscribe<ElectionMessage.ElectionsDone>(gossip);
			}
			// kestrel
			AddTasks(_workersHandler.Start());
			AddTask(_mainQueue.Start());
			AddTask(monitoringQueue.Start());
			AddTask(subscrQueue.Start());
			AddTask(perSubscrQueue.Start());

			if (subsystems != null) {
				foreach (var subsystem in subsystems) {
					var http = new[] { _httpService };
					subsystem.Register(new StandardComponents(db, _mainQueue, _mainBus, _timerService, _timeProvider,
						httpSendService, http, _workersHandler, _queueStatsManager));
				}
			}

			_startup = new ClusterVNodeStartup(_subsystems, _mainQueue, _mainBus, _workersHandler, httpAuthenticationProviders, _authorizationProvider, _readIndex,
				_vNodeSettings.MaxAppendSize, _httpService);
			_mainBus.Subscribe<SystemMessage.SystemReady>(_startup);
			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(_startup);
		}

		private void SubscribeWorkers(Action<InMemoryBus> setup) {
			foreach (var workerBus in _workerBuses) {
				setup(workerBus);
			}
		}

		public void Start() {
			_mainQueue.Publish(new SystemMessage.SystemInit());
		}

		public async Task StopAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default) {
			if (Interlocked.Exchange(ref _stopCalled, 1) == 1) {
				Log.Warning("Stop was already called.");
				return;
			}

			timeout ??= TimeSpan.FromSeconds(5);
			_mainQueue.Publish(new ClientMessage.RequestShutdown(false, true));

			if (_subsystems != null) {
				foreach (var subsystem in _subsystems) {
					subsystem.Stop();
				}
			}

			var cts = new CancellationTokenSource();

			await using var _ = cts.Token.Register(() => _shutdownSource.TrySetCanceled(cancellationToken));

			cts.CancelAfter(timeout.Value);
			await _shutdownSource.Task.ConfigureAwait(false);
		}

		public void Handle(SystemMessage.StateChangeMessage message) {
			OnNodeStatusChanged(new VNodeStatusChangeArgs(message.State));
		}

		public void Handle(SystemMessage.BecomeShuttingDown message) {
			if (_subsystems == null)
				return;
			foreach (var subsystem in _subsystems)
				subsystem.Stop();
		}

		public void Handle(SystemMessage.BecomeShutdown message) {
			_shutdownSource.TrySetResult(true);
		}
		
		public void Handle(SystemMessage.SystemStart message) {
			_authenticationProvider.Initialize().ContinueWith(t => {
				if (t.Exception != null) {
					_mainQueue.Publish(new AuthenticationMessage.AuthenticationProviderInitializationFailed());
				} else {
					_mainQueue.Publish(new AuthenticationMessage.AuthenticationProviderInitialized());
				}
			});
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

		public async Task<ClusterVNode> StartAsync(bool waitUntilReady) {
			var tcs = new TaskCompletionSource<ClusterVNode>(TaskCreationOptions.RunContinuationsAsynchronously);

			if (waitUntilReady) {
				_mainBus.Subscribe(new AdHocHandler<SystemMessage.SystemReady>(
					_ => tcs.TrySetResult(this)));
			} else {
				tcs.TrySetResult(this);
			}

			Start();

			return await tcs.Task.ConfigureAwait(false);
		}

		public static ValueTuple<bool, string> ValidateServerCertificateWithTrustedRootCerts(X509Certificate certificate,
			X509Chain chain, SslPolicyErrors sslPolicyErrors, X509Certificate2Collection trustedRootCerts) {
			return ValidateCertificateWithTrustedRootCerts(certificate, chain, sslPolicyErrors, trustedRootCerts,"server");
		}

		public static ValueTuple<bool, string> ValidateClientCertificateWithTrustedRootCerts(X509Certificate certificate,
			X509Chain chain, SslPolicyErrors sslPolicyErrors, X509Certificate2Collection trustedRootCerts) {
			return ValidateCertificateWithTrustedRootCerts(certificate, chain, sslPolicyErrors, trustedRootCerts,"client");
		}

		private static ValueTuple<bool, string> ValidateCertificateWithTrustedRootCerts(X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors, X509Certificate2Collection trustedRootCerts, string certificateOrigin) {
			if (certificate == null)
				return (false, $"No certificate was provided by the {certificateOrigin}");

			var newChain = new X509Chain {
				ChainPolicy = {
					RevocationMode = X509RevocationMode.NoCheck
				}
			};

			if (trustedRootCerts != null) {
				foreach (var cert in trustedRootCerts)
					newChain.ChainPolicy.ExtraStore.Add(cert);
			}

			newChain.Build(new X509Certificate2(certificate));
			var chainStatus = X509ChainStatusFlags.NoError;
			foreach (var status in newChain.ChainStatus) {
				chainStatus |= status.Status;
			}

			chainStatus &= ~X509ChainStatusFlags.UntrustedRoot; //clear the UntrustedRoot flag which indicates that the certificate is present in the extra store

			if (chainStatus == X509ChainStatusFlags.NoError)
				sslPolicyErrors &= ~SslPolicyErrors.RemoteCertificateChainErrors; //clear the RemoteCertificateChainErrors flag
			else
				sslPolicyErrors |= SslPolicyErrors.RemoteCertificateChainErrors; //set the RemoteCertificateChainErrors flag

			if (sslPolicyErrors != SslPolicyErrors.None) {
				return (false, $"The certificate provided by the {certificateOrigin} failed validation with the following error(s): {sslPolicyErrors.ToString()} ({chainStatus})");
			}

			//client certificates need to be strictly validated against the set of trusted root certificates
			//but this is not required for server certificates since the client is already validating the CN/SAN against the IP address/hostname it's connecting to
			if (certificateOrigin == "client") {
				var chainRoot = newChain.ChainElements[^1].Certificate;
				var chainRootIsTrusted = false;
				if (trustedRootCerts != null) {
					foreach (var rootCert in trustedRootCerts) {
						if (chainRoot.RawData.SequenceEqual(rootCert.RawData)) {
							chainRootIsTrusted = true;
							break;
						}
					}
				}

				if (!chainRootIsTrusted) {
					return (false,
						$"The certificate provided by the {certificateOrigin} does not have a root certificate present in the list of trusted root certificates");
				}
			}

			return (true, null);
		}

		public override string ToString() {
			return
				$"[{_nodeInfo.InstanceId:B}, {_nodeInfo.InternalTcp}, {_nodeInfo.ExternalTcp}, {_nodeInfo.HttpEndPoint}]";
		}
	}
}
