using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
using EventStore.Core.LogAbstraction;
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
using EventStore.Common.Exceptions;
using EventStore.Common.Log;
using EventStore.Common.Options;
using EventStore.Core.Authentication.DelegatedAuthentication;
using EventStore.Core.Authentication.PassthroughAuthentication;
using EventStore.Core.Authorization;
using EventStore.Core.Cluster;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.Util;
using EventStore.Native.UnixSignalManager;
using EventStore.Plugins.Authentication;
using EventStore.Plugins.Authorization;
using Microsoft.AspNetCore.Hosting;
using Mono.Unix.Native;
using ILogger = Serilog.ILogger;
using MidFunc = System.Func<
	Microsoft.AspNetCore.Http.HttpContext,
	System.Func<System.Threading.Tasks.Task>,
	System.Threading.Tasks.Task
>;

namespace EventStore.Core {
	public abstract class ClusterVNode {
		protected static readonly ILogger Log = Serilog.Log.ForContext<ClusterVNode>();

		public static ClusterVNode<TStreamId> Create<TStreamId>(
			ClusterVNodeOptions options,
			LogFormatAbstractor<TStreamId> logFormat,
			AuthenticationProviderFactory authenticationProviderFactory = null,
			AuthorizationProviderFactory authorizationProviderFactory = null,
			IReadOnlyList<IPersistentSubscriptionConsumerStrategyFactory> factories = null,
			Guid? instanceId = null,
			int debugIndex = 0) {

			return new ClusterVNode<TStreamId>(
				options,
				logFormat,
				authenticationProviderFactory,
				authorizationProviderFactory,
				factories,
				instanceId,
				debugIndex);
		}

		abstract public TFChunkDb Db { get; }
		abstract public GossipAdvertiseInfo GossipAdvertiseInfo { get; }
		abstract public IQueuedHandler MainQueue { get; }
		abstract public ISubscriber MainBus { get; }
		abstract public IReadIndex ReadIndex { get; }
		abstract public QueueStatsManager QueueStatsManager { get; }
		abstract public IStartup Startup { get; }
		abstract public IAuthenticationProvider AuthenticationProvider { get; }
		abstract public AuthorizationGateway AuthorizationGateway { get; }
		abstract public IHttpService HttpService { get; }
		abstract public VNodeInfo NodeInfo { get; }
		abstract public Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> InternalClientCertificateValidator { get; }
		abstract public Func<X509Certificate2> CertificateSelector { get; }
		abstract public bool DisableHttps { get; }
		abstract public void Start();
		abstract public Task<ClusterVNode> StartAsync(bool waitUntilRead);
		abstract public Task StopAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);
	}

	public class ClusterVNode<TStreamId> :
		ClusterVNode,
		IHandle<SystemMessage.StateChangeMessage>,
		IHandle<SystemMessage.BecomeShuttingDown>,
		IHandle<SystemMessage.BecomeShutdown>,
		IHandle<SystemMessage.SystemStart>,
		IHandle<ClientMessage.ReloadConfig>{
		private readonly ClusterVNodeOptions _options;
		public override TFChunkDb Db { get; }

		public override GossipAdvertiseInfo GossipAdvertiseInfo { get; }

		public override IQueuedHandler MainQueue {
			get { return _mainQueue; }
		}

		public override ISubscriber MainBus {
			get { return _mainBus; }
		}

		public override IHttpService HttpService {
			get { return _httpService; }
		}

		public override IReadIndex ReadIndex => _readIndex;

		public TimerService TimerService {
			get { return _timerService; }
		}

		public IPublisher NetworkSendService {
			get { return _workersHandler; }
		}

		public override QueueStatsManager QueueStatsManager => _queueStatsManager;

		public override IStartup Startup => _startup;
		
		public override IAuthenticationProvider AuthenticationProvider {
			get { return _authenticationProvider; }
		}

		public override AuthorizationGateway AuthorizationGateway { get; }

		internal MultiQueuedHandler WorkersHandler {
			get { return _workersHandler; }
		}

		public override VNodeInfo NodeInfo { get; }

		public IEnumerable<ISubsystem> Subsystems => _subsystems;

		private readonly IQueuedHandler _mainQueue;
		private readonly ISubscriber _mainBus;

		private readonly ClusterVNodeController<TStreamId> _controller;
		private readonly TimerService _timerService;
		private readonly KestrelHttpService _httpService;
		private readonly ITimeProvider _timeProvider;
		private readonly ISubsystem[] _subsystems;
		private readonly TaskCompletionSource<bool> _shutdownSource = new TaskCompletionSource<bool>();
		private readonly IAuthenticationProvider _authenticationProvider;
		private readonly IAuthorizationProvider _authorizationProvider;
		private readonly IReadIndex<TStreamId> _readIndex;

		private readonly InMemoryBus[] _workerBuses;
		private readonly MultiQueuedHandler _workersHandler;
		public event EventHandler<VNodeStatusChangeArgs> NodeStatusChanged;
		private readonly List<Task> _tasks = new List<Task>();
		private readonly QueueStatsManager _queueStatsManager;
		private X509Certificate2 _certificate;
		private X509Certificate2Collection _trustedRootCerts;
		private readonly bool _disableHttps;
		private readonly Func<X509Certificate2> _certificateSelector;
		private readonly Func<X509Certificate2Collection> _trustedRootCertsSelector;
		private readonly Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> _internalServerCertificateValidator;
		private readonly Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> _internalClientCertificateValidator;
		private readonly Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> _externalClientCertificateValidator;
		private readonly Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> _externalServerCertificateValidator;

		private readonly ClusterVNodeStartup<TStreamId> _startup;
		private readonly EventStoreClusterClientCache _eventStoreClusterClientCache;


		private int _stopCalled;
		private int _reloadingConfig;

		public IEnumerable<Task> Tasks {
			get { return _tasks; }
		}

		public override Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> InternalClientCertificateValidator => _internalClientCertificateValidator;
		public override Func<X509Certificate2> CertificateSelector => _certificateSelector;
		public override bool DisableHttps => _disableHttps;

#if DEBUG
		public TaskCompletionSource<bool> _taskAddedTrigger = new TaskCompletionSource<bool>();
		public object _taskAddLock = new object();
#endif

		protected virtual void OnNodeStatusChanged(VNodeStatusChangeArgs e) {
			EventHandler<VNodeStatusChangeArgs> handler = NodeStatusChanged;
			if (handler != null)
				handler(this, e);
		}

		public ClusterVNode(ClusterVNodeOptions options,
			LogFormatAbstractor<TStreamId> logFormat,
			AuthenticationProviderFactory authenticationProviderFactory = null,
			AuthorizationProviderFactory authorizationProviderFactory = null,
			IReadOnlyList<IPersistentSubscriptionConsumerStrategyFactory>
				additionalPersistentSubscriptionConsumerStrategyFactories = null,
			Guid? instanceId = null, int debugIndex = 0) {

			if (options == null) {
				throw new ArgumentNullException(nameof(options));
			}

			ReloadLogOptions(options);

			instanceId ??= Guid.NewGuid();
			if (instanceId == Guid.Empty) {
				throw new ArgumentException("InstanceId may not be empty.", nameof(instanceId));
			}

			if (options.Interface.ExtIp == null) {
				throw new ArgumentNullException(nameof(options.Interface.ExtIp));
			}

			if (options.Interface.IntIp == null) {
				throw new ArgumentNullException(nameof(options.Interface.IntIp));
			}

			if (options.Cluster.ClusterSize <= 0) {
				throw new ArgumentOutOfRangeException(nameof(options.Cluster.ClusterSize), options.Cluster.ClusterSize,
					$"{nameof(options.Cluster.ClusterSize)} must be greater than 0.");
			}

			if (!options.Application.Insecure) {
				ReloadCertificates(options);

				if (_trustedRootCerts == null || _certificate == null) {
					throw new InvalidConfigurationException("A certificate is required unless insecure mode (--insecure) is set.");
				}
			}

			if (options.Application.WorkerThreads <= 0) {
				throw new ArgumentOutOfRangeException(nameof(options.Application.WorkerThreads),
					options.Application.WorkerThreads,
					$"{nameof(options.Application.WorkerThreads)} must be greater than 0.");
			}

			if (options.Cluster.ClusterDns == null) {
				throw new ArgumentNullException(nameof(options.Cluster.ClusterDns));
			}

			if (options.Cluster.GossipSeed == null) {
				throw new ArgumentNullException(nameof(options.Cluster.GossipSeed));
			}

			if (options.Cluster.PrepareAckCount <= 0) {
				throw new ArgumentOutOfRangeException(nameof(options.Cluster.PrepareAckCount),
					options.Cluster.PrepareAckCount,
					$"{nameof(options.Cluster.PrepareAckCount)} must be greater than 0.");
			}

			if (options.Cluster.CommitAckCount <= 0) {
				throw new ArgumentOutOfRangeException(nameof(options.Cluster.CommitAckCount),
					options.Cluster.CommitAckCount,
					$"{nameof(options.Cluster.CommitAckCount)} must be greater than 0.");
			}

			if (options.Database.InitializationThreads <= 0) {
				throw new ArgumentOutOfRangeException(nameof(options.Database.InitializationThreads),
					options.Database.InitializationThreads,
					$"{nameof(options.Database.InitializationThreads)} must be greater than 0.");
			}

			if (options.Grpc.KeepAliveTimeout <= 0) {
				throw new ArgumentOutOfRangeException(nameof(options.Grpc.KeepAliveTimeout));
			}

			if (options.Grpc.KeepAliveInterval <= 0) {
				throw new ArgumentOutOfRangeException(nameof(options.Grpc.KeepAliveInterval));
			}

			if (options.Application.MaxAppendSize > TFConsts.EffectiveMaxLogRecordSize) {
				throw new ArgumentOutOfRangeException(nameof(options.Application.MaxAppendSize),
					$"{nameof(options.Application.MaxAppendSize)} exceeded {TFConsts.EffectiveMaxLogRecordSize} bytes.");
			}

			if (options.Cluster.DiscoverViaDns && string.IsNullOrWhiteSpace(options.Cluster.ClusterDns))
				throw new ArgumentException(
					"Either DNS Discovery must be disabled (and seeds specified), or a cluster DNS name must be provided.");

			if (options.Database.Db.StartsWith("~")) {
				throw new ApplicationInitializationException(
					"The given database path starts with a '~'. Event Store does not expand '~'.");
			}

			if (options.Database.Index != null && options.Database.Db != null) {
				string absolutePathIndex = Path.GetFullPath(options.Database.Index);
				string absolutePathDb = Path.GetFullPath(options.Database.Db);
				if (absolutePathDb.Equals(absolutePathIndex)) {
					throw new ApplicationInitializationException(
						$"The given database ({absolutePathDb}) and index ({absolutePathIndex}) paths cannot point to the same directory.");
				}
			}

			if (options.Cluster.GossipSeed.Length > 1 && options.Cluster.ClusterSize == 1) {
				throw new ApplicationInitializationException(
					"The given ClusterSize is set to 1 but GossipSeeds are multiple. We will never be able to sync up with this configuration.");
			}

			if (options.Cluster.ReadOnlyReplica && options.Cluster.ClusterSize <= 1) {
				throw new InvalidConfigurationException(
					"This node cannot be configured as a Read Only Replica as these node types are only supported in a clustered configuration.");
			}



			_options = options;

#if DEBUG
			AddTask(_taskAddedTrigger.Task);
#endif

			var disableInternalTcpTls = options.Application.Insecure;
			var disableExternalTcpTls = options.Application.Insecure || options.Interface.DisableExternalTcpTls;

			var httpEndPoint = new IPEndPoint(options.Interface.ExtIp, options.Interface.HttpPort);
			var intTcp = disableInternalTcpTls
				? new IPEndPoint(options.Interface.IntIp, options.Interface.IntTcpPort)
				: null;
			var intSecIp = !disableInternalTcpTls
				? new IPEndPoint(options.Interface.IntIp, options.Interface.IntTcpPort)
				: null;

			var extTcp = disableExternalTcpTls
				? new IPEndPoint(options.Interface.ExtIp, options.Interface.ExtTcpPort)
				: null;
			var extSecIp = !disableExternalTcpTls
				? new IPEndPoint(options.Interface.ExtIp, options.Interface.ExtTcpPort)
				: null;

			var intTcpPortAdvertiseAs = disableInternalTcpTls ? options.Interface.IntTcpPortAdvertiseAs : 0;
			var intSecTcpPortAdvertiseAs = !disableInternalTcpTls ? options.Interface.IntTcpPortAdvertiseAs : 0;
			var extTcpPortAdvertiseAs = options.Interface.EnableExternalTcp && disableExternalTcpTls
				? options.Interface.ExtTcpPortAdvertiseAs
				: 0;
			var extSecTcpPortAdvertiseAs = options.Interface.EnableExternalTcp && !disableExternalTcpTls
				? options.Interface.ExtTcpPortAdvertiseAs
				: 0;

			Log.Information("Quorum size set to {quorum}.", options.Cluster.PrepareAckCount);

			NodeInfo = new VNodeInfo(instanceId.Value, debugIndex, intTcp, intSecIp, extTcp, extSecIp,
				httpEndPoint, options.Cluster.ReadOnlyReplica);

			Db = new TFChunkDb(CreateDbConfig());

			TFChunkDbConfig CreateDbConfig() {
				ICheckpoint writerChk;
				ICheckpoint chaserChk;
				ICheckpoint epochChk;
				ICheckpoint proposalChk;
				ICheckpoint truncateChk;
				//todo(clc) : promote these to file backed checkpoints re:project-io
				ICheckpoint replicationChk = new InMemoryCheckpoint(Checkpoint.Replication, initValue: -1);
				ICheckpoint indexChk = new InMemoryCheckpoint(Checkpoint.Replication, initValue: -1);
				var dbPath = options.Database.Db;

				if (options.Database.MemDb) {
					writerChk = new InMemoryCheckpoint(Checkpoint.Writer);
					chaserChk = new InMemoryCheckpoint(Checkpoint.Chaser);
					epochChk = new InMemoryCheckpoint(Checkpoint.Epoch, initValue: -1);
					proposalChk = new InMemoryCheckpoint(Checkpoint.Proposal, initValue: -1);
					truncateChk = new InMemoryCheckpoint(Checkpoint.Truncate, initValue: -1);
				} else {
					try {
						if (!Directory.Exists(dbPath)) // mono crashes without this check
							Directory.CreateDirectory(dbPath);
					} catch (UnauthorizedAccessException) {
						if (dbPath == Locations.DefaultDataDirectory) {
							Log.Information(
								"Access to path {dbPath} denied. The Event Store database will be created in {fallbackDefaultDataDirectory}",
								dbPath, Locations.FallbackDefaultDataDirectory);
							dbPath = Locations.FallbackDefaultDataDirectory;
							Log.Information("Defaulting DB Path to {dbPath}", dbPath);

							if (!Directory.Exists(dbPath)) // mono crashes without this check
								Directory.CreateDirectory(dbPath);
						} else {
							throw;
						}
					}

					var writerCheckFilename = Path.Combine(dbPath, Checkpoint.Writer + ".chk");
					var chaserCheckFilename = Path.Combine(dbPath, Checkpoint.Chaser + ".chk");
					var epochCheckFilename = Path.Combine(dbPath, Checkpoint.Epoch + ".chk");
					var proposalCheckFilename = Path.Combine(dbPath, Checkpoint.Proposal + ".chk");
					var truncateCheckFilename = Path.Combine(dbPath, Checkpoint.Truncate + ".chk");
					writerChk = new MemoryMappedFileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
					chaserChk = new MemoryMappedFileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);
					epochChk = new MemoryMappedFileCheckpoint(epochCheckFilename, Checkpoint.Epoch, cached: true,
						initValue: -1);
					proposalChk = new MemoryMappedFileCheckpoint(proposalCheckFilename, Checkpoint.Proposal,
						cached: true,
						initValue: -1);
					truncateChk = new MemoryMappedFileCheckpoint(truncateCheckFilename, Checkpoint.Truncate,
						cached: true, initValue: -1);
				}

				var cache = options.Database.CachedChunks >= 0
					? options.Database.CachedChunks * (long)(TFConsts.ChunkSize + ChunkHeader.Size + ChunkFooter.Size)
					: options.Database.ChunksCacheSize;

				return new TFChunkDbConfig(dbPath,
					new VersionedPatternFileNamingStrategy(dbPath, "chunk-"),
					options.Database.ChunkSize,
					cache,
					writerChk,
					chaserChk,
					epochChk,
					proposalChk,
					truncateChk,
					replicationChk,
					indexChk,
					options.Database.ChunkInitialReaderCount,
					options.Database.GetTFChunkMaxReaderCount(),
					options.Database.MemDb,
					options.Database.Unbuffered,
					options.Database.WriteThrough,
					options.Database.OptimizeIndexMerge,
					options.Database.ReduceFileCachePressure,
					options.Database.MaxTruncation);
			}

			var writerCheckpoint = Db.Config.WriterCheckpoint.Read();
			var chaserCheckpoint = Db.Config.ChaserCheckpoint.Read();
			var epochCheckpoint = Db.Config.EpochCheckpoint.Read();
			var truncateCheckpoint = Db.Config.TruncateCheckpoint.Read();

			Log.Information("{description,-25} {instanceId}", "INSTANCE ID:", NodeInfo.InstanceId);
			Log.Information("{description,-25} {path}", "DATABASE:", Db.Config.Path);
			Log.Information("{description,-25} {writerCheckpoint} (0x{writerCheckpoint:X})", "WRITER CHECKPOINT:",
				writerCheckpoint, writerCheckpoint);
			Log.Information("{description,-25} {chaserCheckpoint} (0x{chaserCheckpoint:X})", "CHASER CHECKPOINT:",
				chaserCheckpoint, chaserCheckpoint);
			Log.Information("{description,-25} {epochCheckpoint} (0x{epochCheckpoint:X})", "EPOCH CHECKPOINT:",
				epochCheckpoint, epochCheckpoint);
			Log.Information("{description,-25} {truncateCheckpoint} (0x{truncateCheckpoint:X})", "TRUNCATE CHECKPOINT:",
				truncateCheckpoint, truncateCheckpoint);

			var isSingleNode = options.Cluster.ClusterSize == 1;
			_disableHttps = options.Application.Insecure;
			_mainBus = new InMemoryBus("MainBus");
			_queueStatsManager = new QueueStatsManager();

			_certificateSelector = () => _certificate;
			_trustedRootCertsSelector = () => _trustedRootCerts;

			_internalServerCertificateValidator = (cert, chain, errors) =>  ValidateServerCertificateWithTrustedRootCerts(cert, chain, errors, _trustedRootCertsSelector);
			_internalClientCertificateValidator = (cert, chain, errors) =>  ValidateClientCertificateWithTrustedRootCerts(cert, chain, errors, _trustedRootCertsSelector);
			_externalClientCertificateValidator = delegate { return (true, null); };
			_externalServerCertificateValidator = (cert, chain, errors) => ValidateServerCertificateWithTrustedRootCerts(cert, chain, errors, _trustedRootCertsSelector);

			var forwardingProxy = new MessageForwardingProxy();
			if (options.Application.EnableHistograms) {
				HistogramService.CreateHistograms();
				//start watching jitter
				HistogramService.StartJitterMonitor();
			}

			// MISC WORKERS
			_workerBuses = Enumerable.Range(0, options.Application.WorkerThreads).Select(queueNum =>
				new InMemoryBus($"Worker #{queueNum + 1} Bus",
					watchSlowMsg: true,
					slowMsgThreshold: TimeSpan.FromMilliseconds(200))).ToArray();
			_workersHandler = new MultiQueuedHandler(
				options.Application.WorkerThreads,
				queueNum => new QueuedHandlerThreadPool(_workerBuses[queueNum],
					$"Worker #{queueNum + 1}",
					_queueStatsManager,
					groupName: "Workers",
					watchSlowMsg: true,
					slowMsgThreshold: TimeSpan.FromMilliseconds(200)));

			_subsystems = options.Subsystems.ToArray();

			_controller =
				new ClusterVNodeController<TStreamId>((IPublisher)_mainBus, NodeInfo, Db, options, this, forwardingProxy);
			_mainQueue = QueuedHandler.CreateQueuedHandler(_controller, "MainQueue", _queueStatsManager);

			_controller.SetMainQueue(_mainQueue);

			_eventStoreClusterClientCache = new EventStoreClusterClientCache(_mainQueue,
				(endpoint, publisher) =>
					new EventStoreClusterClient(
						new UriBuilder(options.Application.Insecure ? Uri.UriSchemeHttp : Uri.UriSchemeHttps,
							endpoint.GetHost(), endpoint.GetPort()).Uri, publisher, _internalServerCertificateValidator,
						_certificateSelector));

			_mainBus.Subscribe<ClusterClientMessage.CleanCache>(_eventStoreClusterClientCache);
			_mainBus.Subscribe<SystemMessage.SystemInit>(_eventStoreClusterClientCache);

			//SELF
			_mainBus.Subscribe<SystemMessage.StateChangeMessage>(this);
			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(this);
			_mainBus.Subscribe<SystemMessage.BecomeShutdown>(this);
			_mainBus.Subscribe<SystemMessage.SystemStart>(this);
			_mainBus.Subscribe<ClientMessage.ReloadConfig>(this);

			// MONITORING
			var monitoringInnerBus = new InMemoryBus("MonitoringInnerBus", watchSlowMsg: false);
			var monitoringRequestBus = new InMemoryBus("MonitoringRequestBus", watchSlowMsg: false);
			var monitoringQueue = new QueuedHandlerThreadPool(monitoringInnerBus, "MonitoringQueue", _queueStatsManager, true,
				TimeSpan.FromMilliseconds(800));
			var monitoring = new MonitoringService(monitoringQueue,
				monitoringRequestBus,
				_mainQueue,
				Db.Config.WriterCheckpoint.AsReadOnly(),
				Db.Config.Path,
				TimeSpan.FromSeconds(options.Application.StatsPeriodSec),
				NodeInfo.HttpEndPoint,
				options.Database.StatsStorage,
				NodeInfo.ExternalTcp,
				NodeInfo.ExternalSecureTcp);
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

			// TRUNCATE IF NECESSARY
			var truncPos = Db.Config.TruncateCheckpoint.Read();
			if (truncPos != -1) {
				Log.Information(
					"Truncate checkpoint is present. Truncate: {truncatePosition} (0x{truncatePosition:X}), Writer: {writerCheckpoint} (0x{writerCheckpoint:X}), Chaser: {chaserCheckpoint} (0x{chaserCheckpoint:X}), Epoch: {epochCheckpoint} (0x{epochCheckpoint:X})",
					truncPos, truncPos, writerCheckpoint, writerCheckpoint, chaserCheckpoint, chaserCheckpoint,
					epochCheckpoint, epochCheckpoint);
				var truncator = new TFChunkDbTruncator(Db.Config);
				truncator.TruncateDb(truncPos);
			}

			// STORAGE SUBSYSTEM
			Db.Open(!options.Database.SkipDbVerify, threads: options.Database.InitializationThreads);
			var indexPath = options.Database.Index ?? Path.Combine(Db.Config.Path, "index");

			var readerPool = new ObjectPool<ITransactionFileReader>(
				"ReadIndex readers pool",
				ESConsts.PTableInitialReaderCount,
				options.Database.GetPTableMaxReaderCount(),
				() => new TFChunkReader(
					Db,
					Db.Config.WriterCheckpoint.AsReadOnly(),
					optimizeReadSideCache: Db.Config.OptimizeReadSideCache));

			var tableIndex = new TableIndex<TStreamId>(indexPath,
				logFormat.LowHasher,
				logFormat.HighHasher,
				logFormat.EmptyStreamId,
				() => new HashListMemTable(options.IndexBitnessVersion,
					maxSize: options.Database.MaxMemTableSize * 2),
				() => new TFReaderLease(readerPool),
				options.IndexBitnessVersion,
				maxSizeForMemory: options.Database.MaxMemTableSize,
				maxTablesPerLevel: 2,
				inMem: Db.Config.InMemDb,
				skipIndexVerify: options.Database.SkipIndexVerify,
				indexCacheDepth: options.Database.IndexCacheDepth,
				initializationThreads: options.Database.InitializationThreads,
				additionalReclaim: false,
				maxAutoMergeIndexLevel: options.Database.MaxAutoMergeIndexLevel,
				pTableMaxReaderCount: options.Database.GetPTableMaxReaderCount());
			var readIndex = new ReadIndex<TStreamId>(_mainQueue,
				readerPool,
				tableIndex,
				logFormat.StreamIds,
				logFormat.StreamNamesProvider,
				logFormat.EmptyStreamId,
				logFormat.StreamIdValidator,
				logFormat.StreamIdSizer,
				options.Cluster.StreamInfoCacheCapacity,
				ESConsts.PerformAdditionlCommitChecks,
				ESConsts.MetaStreamMaxCount,
				options.Database.HashCollisionReadLimit,
				options.Application.SkipIndexScanOnReads,
				Db.Config.ReplicationCheckpoint.AsReadOnly(),
				Db.Config.IndexCheckpoint);
			_readIndex = readIndex;
			var writer = new TFChunkWriter(Db);
			var epochManager = new EpochManager(_mainQueue,
				ESConsts.CachedEpochCount,
				Db.Config.EpochCheckpoint,
				writer,
				initialReaderCount: 1,
				maxReaderCount: 5,
				readerFactory: () => new TFChunkReader(
					Db,
					Db.Config.WriterCheckpoint.AsReadOnly(),
					optimizeReadSideCache: Db.Config.OptimizeReadSideCache),
				logFormat.RecordFactory,
				NodeInfo.InstanceId);
			epochManager.Init();

			var storageWriter = new ClusterStorageWriterService<TStreamId>(_mainQueue, _mainBus,
				TimeSpan.FromMilliseconds(options.Database.MinFlushDelayMs), Db, writer, readIndex.IndexWriter,
				logFormat.RecordFactory,
				logFormat.StreamNameIndex,
				logFormat.SystemStreams,
				epochManager, _queueStatsManager, () => readIndex.LastIndexedPosition); // subscribes internally
			AddTasks(storageWriter.Tasks);

			monitoringRequestBus.Subscribe<MonitoringMessage.InternalStatsRequest>(storageWriter);

			var storageReader = new StorageReaderService<TStreamId>(_mainQueue, _mainBus, readIndex,
				logFormat.SystemStreams,
				options.Database.ReaderThreadsCount, Db.Config.WriterCheckpoint.AsReadOnly(), _queueStatsManager);

			_mainBus.Subscribe<SystemMessage.SystemInit>(storageReader);
			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(storageReader);
			_mainBus.Subscribe<SystemMessage.BecomeShutdown>(storageReader);
			monitoringRequestBus.Subscribe<MonitoringMessage.InternalStatsRequest>(storageReader);


			// REPLICATION TRACKING
			var replicationTracker = new ReplicationTrackingService(
				_mainQueue,
				options.Cluster.ClusterSize,
				Db.Config.ReplicationCheckpoint,
				Db.Config.WriterCheckpoint.AsReadOnly());
			AddTask(replicationTracker.Task);
			_mainBus.Subscribe<SystemMessage.SystemInit>(replicationTracker);
			_mainBus.Subscribe<SystemMessage.StateChangeMessage>(replicationTracker);
			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(replicationTracker);
			_mainBus.Subscribe<ReplicationTrackingMessage.ReplicaWriteAck>(replicationTracker);
			_mainBus.Subscribe<ReplicationTrackingMessage.WriterCheckpointFlushed>(replicationTracker);
			_mainBus.Subscribe<ReplicationTrackingMessage.LeaderReplicatedTo>(replicationTracker);
			_mainBus.Subscribe<SystemMessage.VNodeConnectionLost>(replicationTracker);

			var indexCommitterService = new IndexCommitterService<TStreamId>(readIndex.IndexCommitter, _mainQueue,
				Db.Config.WriterCheckpoint.AsReadOnly(),
				Db.Config.ReplicationCheckpoint.AsReadOnly(),
				options.Cluster.CommitAckCount, tableIndex, _queueStatsManager);

			AddTask(indexCommitterService.Task);

			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(indexCommitterService);
			_mainBus.Subscribe<ReplicationTrackingMessage.ReplicatedTo>(indexCommitterService);
			_mainBus.Subscribe<StorageMessage.CommitAck>(indexCommitterService);
			_mainBus.Subscribe<ClientMessage.MergeIndexes>(indexCommitterService);

			var chaser = new TFChunkChaser(
				Db,
				Db.Config.WriterCheckpoint.AsReadOnly(),
				Db.Config.ChaserCheckpoint,
				Db.Config.OptimizeReadSideCache);

			var storageChaser = new StorageChaser<TStreamId>(
				_mainQueue,
				Db.Config.WriterCheckpoint.AsReadOnly(),
				chaser,
				indexCommitterService,
				epochManager,
				_queueStatsManager);
			AddTask(storageChaser.Task);

#if DEBUG
			_queueStatsManager.InitializeCheckpoints(
				Db.Config.WriterCheckpoint.AsReadOnly(),
				Db.Config.ChaserCheckpoint.AsReadOnly());
#endif
			_mainBus.Subscribe<SystemMessage.SystemInit>(storageChaser);
			_mainBus.Subscribe<SystemMessage.SystemStart>(storageChaser);
			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(storageChaser);
			// REPLICATION TRACKING END

			var httpPipe = new HttpMessagePipe();
			var httpSendService = new HttpSendService(httpPipe, true, _externalServerCertificateValidator);
			_mainBus.Subscribe<SystemMessage.StateChangeMessage>(httpSendService);
			SubscribeWorkers(bus => bus.Subscribe<HttpMessage.HttpSend>(httpSendService));

			var grpcSendService = new GrpcSendService(_eventStoreClusterClientCache);
			_mainBus.Subscribe(new WideningHandler<GrpcMessage.SendOverGrpc, Message>(_workersHandler));
			SubscribeWorkers(bus => {
				bus.Subscribe<GrpcMessage.SendOverGrpc>(grpcSendService);
			});

			GossipAdvertiseInfo = GetGossipAdvertiseInfo();
			GossipAdvertiseInfo GetGossipAdvertiseInfo() {
				IPAddress intIpAddress = options.Interface.IntIp; //this value is just opts.IntIP

				var extIpAddress = options.Interface.ExtIp; //this value is just opts.ExtIP

				var intHostToAdvertise = options.Interface.IntHostAdvertiseAs ?? intIpAddress.ToString();
				var extHostToAdvertise = options.Interface.ExtHostAdvertiseAs ?? extIpAddress.ToString();

				if (intIpAddress.Equals(IPAddress.Any) || extIpAddress.Equals(IPAddress.Any)) {
					IPAddress nonLoopbackAddress = IPFinder.GetNonLoopbackAddress();
					IPAddress addressToAdvertise = options.Cluster.ClusterSize > 1 ? nonLoopbackAddress : IPAddress.Loopback;

					if (intIpAddress.Equals(IPAddress.Any) && options.Interface.IntHostAdvertiseAs == null) {
						intHostToAdvertise = addressToAdvertise.ToString();
					}

					if (extIpAddress.Equals(IPAddress.Any) && options.Interface.ExtHostAdvertiseAs == null) {
						extHostToAdvertise = addressToAdvertise.ToString();
					}
				}

				var intTcpEndPoint = NodeInfo.InternalTcp == null
					? null
					: new DnsEndPoint(intHostToAdvertise, intTcpPortAdvertiseAs > 0
						? options.Interface.IntTcpPortAdvertiseAs
						: NodeInfo.InternalTcp.Port);

				var intSecureTcpEndPoint = NodeInfo.InternalSecureTcp == null
					? null
					: new DnsEndPoint(intHostToAdvertise, intSecTcpPortAdvertiseAs > 0
						? intSecTcpPortAdvertiseAs
						: NodeInfo.InternalSecureTcp.Port);

				var extTcpEndPoint = NodeInfo.ExternalTcp == null
					? null
					: new DnsEndPoint(extHostToAdvertise, extTcpPortAdvertiseAs > 0
						? extTcpPortAdvertiseAs
						: NodeInfo.ExternalTcp.Port);

				var extSecureTcpEndPoint = NodeInfo.ExternalSecureTcp == null
					? null
					: new DnsEndPoint(extHostToAdvertise, extSecTcpPortAdvertiseAs > 0
						? extSecTcpPortAdvertiseAs
						: NodeInfo.ExternalSecureTcp.Port);

				var httpEndPoint = new DnsEndPoint(extHostToAdvertise,
					options.Interface.HttpPortAdvertiseAs > 0
						? options.Interface.HttpPortAdvertiseAs
						: NodeInfo.HttpEndPoint.GetPort());

				return new GossipAdvertiseInfo(intTcpEndPoint, intSecureTcpEndPoint, extTcpEndPoint,
					extSecureTcpEndPoint, httpEndPoint, options.Interface.IntHostAdvertiseAs,
					options.Interface.ExtHostAdvertiseAs, options.Interface.HttpPortAdvertiseAs,
					options.Interface.AdvertiseHostToClientAs, options.Interface.AdvertiseHttpPortToClientAs,
					options.Interface.AdvertiseTcpPortToClientAs);
			}

			_httpService = new KestrelHttpService(ServiceAccessibility.Public, _mainQueue, new TrieUriRouter(),
				_workersHandler, options.Application.LogHttpRequests,
				GossipAdvertiseInfo.AdvertiseExternalHostAs,
				GossipAdvertiseInfo.AdvertiseHttpPortAs,
				options.Auth.DisableFirstLevelHttpAuthorization,
				NodeInfo.HttpEndPoint);

			var components = new AuthenticationProviderFactoryComponents {
				MainBus = _mainBus,
				MainQueue = _mainQueue,
				WorkerBuses = _workerBuses,
				WorkersQueue = _workersHandler,
				HttpSendService = httpSendService,
				HttpService = _httpService,
			};

			// AUTHENTICATION INFRASTRUCTURE - delegate to plugins
			authorizationProviderFactory ??= !options.Application.Insecure
				? throw new InvalidConfigurationException($"An {nameof(AuthorizationProviderFactory)} is required when running securely.")
				: new AuthorizationProviderFactory(_ => new PassthroughAuthorizationProviderFactory());
			authenticationProviderFactory ??= !options.Application.Insecure
				? throw new InvalidConfigurationException($"An {nameof(AuthenticationProviderFactory)} is required when running securely.")
				: new AuthenticationProviderFactory(_ => new PassthroughAuthenticationProviderFactory());
			additionalPersistentSubscriptionConsumerStrategyFactories ??=
				Array.Empty<IPersistentSubscriptionConsumerStrategyFactory>();

			_authenticationProvider = new DelegatedAuthenticationProvider(
				authenticationProviderFactory.GetFactory(components).Build(
					options.Application.LogFailedAuthenticationAttempts, Log));
			Ensure.NotNull(_authenticationProvider, nameof(_authenticationProvider));

			_authorizationProvider = authorizationProviderFactory
				.GetFactory(new AuthorizationProviderFactoryComponents {
					MainQueue = _mainQueue
				}).Build();
			Ensure.NotNull(_authorizationProvider, "authorizationProvider");

			AuthorizationGateway = new AuthorizationGateway(_authorizationProvider);
			{
				// EXTERNAL TCP
				if (NodeInfo.ExternalTcp != null && options.Interface.EnableExternalTcp) {
					var extTcpService = new TcpService(_mainQueue, NodeInfo.ExternalTcp, _workersHandler,
						TcpServiceType.External, TcpSecurityType.Normal,
						new ClientTcpDispatcher(TimeSpan.FromMilliseconds(options.Database.WriteTimeoutMs)),
						TimeSpan.FromMilliseconds(options.Interface.ExtTcpHeartbeatInterval),
						TimeSpan.FromMilliseconds(options.Interface.ExtTcpHeartbeatTimeout),
						_authenticationProvider, AuthorizationGateway, null, null,
						options.Interface.ConnectionPendingSendBytesThreshold,
						options.Interface.ConnectionQueueSizeThreshold);
					_mainBus.Subscribe<SystemMessage.SystemInit>(extTcpService);
					_mainBus.Subscribe<SystemMessage.SystemStart>(extTcpService);
					_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(extTcpService);
				}
				// EXTERNAL SECURE TCP
				if (NodeInfo.ExternalSecureTcp != null && options.Interface.EnableExternalTcp) {
					var extSecTcpService = new TcpService(_mainQueue, NodeInfo.ExternalSecureTcp, _workersHandler,
						TcpServiceType.External, TcpSecurityType.Secure,
						new ClientTcpDispatcher(TimeSpan.FromMilliseconds(options.Database.WriteTimeoutMs)),
						TimeSpan.FromMilliseconds(options.Interface.ExtTcpHeartbeatInterval),
						TimeSpan.FromMilliseconds(options.Interface.ExtTcpHeartbeatTimeout),
						_authenticationProvider, AuthorizationGateway, _certificateSelector, _externalClientCertificateValidator,
						options.Interface.ConnectionPendingSendBytesThreshold,
						options.Interface.ConnectionQueueSizeThreshold);
					_mainBus.Subscribe<SystemMessage.SystemInit>(extSecTcpService);
					_mainBus.Subscribe<SystemMessage.SystemStart>(extSecTcpService);
					_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(extSecTcpService);
				}

				if (!isSingleNode) {
					// INTERNAL TCP
					if (NodeInfo.InternalTcp != null) {
						var intTcpService = new TcpService(_mainQueue, NodeInfo.InternalTcp, _workersHandler,
							TcpServiceType.Internal, TcpSecurityType.Normal,
							new InternalTcpDispatcher(TimeSpan.FromMilliseconds(options.Database.WriteTimeoutMs)),
							TimeSpan.FromMilliseconds(options.Interface.IntTcpHeartbeatInterval),
							TimeSpan.FromMilliseconds(options.Interface.IntTcpHeartbeatTimeout),
							_authenticationProvider, AuthorizationGateway, null, null, ESConsts.UnrestrictedPendingSendBytes,
						ESConsts.MaxConnectionQueueSize);
						_mainBus.Subscribe<SystemMessage.SystemInit>(intTcpService);
						_mainBus.Subscribe<SystemMessage.SystemStart>(intTcpService);
						_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(intTcpService);
					}
					// INTERNAL SECURE TCP
					if (NodeInfo.InternalSecureTcp != null) {
						var intSecTcpService = new TcpService(_mainQueue, NodeInfo.InternalSecureTcp, _workersHandler,
							TcpServiceType.Internal, TcpSecurityType.Secure,
							new InternalTcpDispatcher(TimeSpan.FromMilliseconds(options.Database.WriteTimeoutMs)),
							TimeSpan.FromMilliseconds(options.Interface.IntTcpHeartbeatInterval),
							TimeSpan.FromMilliseconds(options.Interface.IntTcpHeartbeatTimeout),
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


			var httpAuthenticationProviders = new List<IHttpAuthenticationProvider>();

			foreach (var authenticationScheme in _authenticationProvider.GetSupportedAuthenticationSchemes() ?? Enumerable.Empty<string>()) {
				switch (authenticationScheme)
				{
					case "Basic":
						httpAuthenticationProviders.Add(new BasicHttpAuthenticationProvider(_authenticationProvider));
						break;
					case "Bearer":
						httpAuthenticationProviders.Add(new BearerHttpAuthenticationProvider(_authenticationProvider));
						break;
					case "Insecure":
						httpAuthenticationProviders.Add(new PassthroughHttpAuthenticationProvider(_authenticationProvider));
						break;
					default:
						Log.Warning($"Unsupported Authentication Scheme: {authenticationScheme}");
						break;
				}
			}

			if (!httpAuthenticationProviders.Any()) {
				throw new InvalidConfigurationException($"The server does not support any authentication scheme supported by the '{_authenticationProvider.Name}' authentication provider.");
			}

			if (!options.Application.Insecure) {
				//transport-level authentication providers
				httpAuthenticationProviders.Add(
					new ClientCertificateAuthenticationProvider(options.Certificate.CertificateReservedNodeCommonName));

				if (options.Interface.EnableTrustedAuth)
					httpAuthenticationProviders.Add(new TrustedHttpAuthenticationProvider());
			}

			//default authentication provider
			httpAuthenticationProviders.Add(new AnonymousHttpAuthenticationProvider());


			var adminController = new AdminController(_mainQueue, _workersHandler);
			var pingController = new PingController();
			var histogramController = new HistogramController();
			var statController = new StatController(monitoringQueue, _workersHandler);
			var atomController = new AtomController(_mainQueue, _workersHandler,
				options.Application.DisableHttpCaching, TimeSpan.FromMilliseconds(options.Database.WriteTimeoutMs));
			var gossipController = new GossipController(_mainQueue, _workersHandler);
			var persistentSubscriptionController =
				new PersistentSubscriptionController(httpSendService, _mainQueue, _workersHandler);
			var infoController = new InfoController(options, new Dictionary<string, bool> {
				["projections"] = options.Projections.RunProjections != ProjectionType.None,
				["userManagement"] = options.Auth.AuthenticationType == Opts.AuthenticationTypeDefault &&
				                     !options.Application.Insecure,
				["atomPub"] = options.Interface.EnableAtomPubOverHttp

			}, _authenticationProvider);

			_mainBus.Subscribe<SystemMessage.StateChangeMessage>(infoController);

			_httpService.SetupController(persistentSubscriptionController);
			if (!options.Interface.DisableAdminUi)
				_httpService.SetupController(adminController);
			_httpService.SetupController(pingController);
			_httpService.SetupController(infoController);
			if (!options.Interface.DisableStatsOnHttp)
				_httpService.SetupController(statController);
			if (options.Interface.EnableAtomPubOverHttp)
				_httpService.SetupController(atomController);
			if (!options.Interface.DisableGossipOnHttp)
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
				TimeSpan.FromMilliseconds(options.Database.PrepareTimeoutMs),
				TimeSpan.FromMilliseconds(options.Database.CommitTimeoutMs),
				logFormat.SupportsExplicitTransactions);

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

			var subscription = new SubscriptionsService<TStreamId>(_mainQueue, subscrQueue, readIndex);
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
			_mainBus.Subscribe<ClientMessage.ReadAllEventsForwardCompleted>(ioDispatcher.AllForwardReader);
			_mainBus.Subscribe<ClientMessage.DeleteStreamCompleted>(ioDispatcher.StreamDeleter);
			_mainBus.Subscribe(ioDispatcher);
			var perSubscrBus = new InMemoryBus("PersistentSubscriptionsBus", true, TimeSpan.FromMilliseconds(50));
			var perSubscrQueue = new QueuedHandlerThreadPool(perSubscrBus, "PersistentSubscriptions", _queueStatsManager, false);
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<SystemMessage.StateChangeMessage, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<TcpMessage.ConnectionClosed, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.CreatePersistentSubscriptionToStream, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.UpdatePersistentSubscriptionToStream, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.DeletePersistentSubscriptionToStream, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.CreatePersistentSubscriptionToAll, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.UpdatePersistentSubscriptionToAll, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.DeletePersistentSubscriptionToAll, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.ConnectToPersistentSubscriptionToStream, Message>());
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<ClientMessage.ConnectToPersistentSubscriptionToAll, Message>());
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
			_mainBus.Subscribe(perSubscrQueue.WidenFrom<SubscriptionMessage.PersistentSubscriptionsRestart, Message>());

			//TODO CC can have multiple threads working on subscription if partition
			var consumerStrategyRegistry = new PersistentSubscriptionConsumerStrategyRegistry(_mainQueue, _mainBus,
				additionalPersistentSubscriptionConsumerStrategyFactories);
			var persistentSubscription = new PersistentSubscriptionService<TStreamId>(perSubscrQueue, readIndex, ioDispatcher,
				_mainQueue, consumerStrategyRegistry);
			perSubscrBus.Subscribe<SystemMessage.BecomeShuttingDown>(persistentSubscription);
			perSubscrBus.Subscribe<SystemMessage.BecomeLeader>(persistentSubscription);
			perSubscrBus.Subscribe<SystemMessage.StateChangeMessage>(persistentSubscription);
			perSubscrBus.Subscribe<TcpMessage.ConnectionClosed>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.ConnectToPersistentSubscriptionToStream>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.ConnectToPersistentSubscriptionToAll>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.UnsubscribeFromStream>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.PersistentSubscriptionAckEvents>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.PersistentSubscriptionNackEvents>(persistentSubscription);
			perSubscrBus.Subscribe<StorageMessage.EventCommitted>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.CreatePersistentSubscriptionToStream>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.UpdatePersistentSubscriptionToStream>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.DeletePersistentSubscriptionToStream>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.CreatePersistentSubscriptionToAll>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.UpdatePersistentSubscriptionToAll>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.DeletePersistentSubscriptionToAll>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.ReplayParkedMessages>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.ReplayParkedMessage>(persistentSubscription);
			perSubscrBus.Subscribe<ClientMessage.ReadNextNPersistentMessages>(persistentSubscription);
			perSubscrBus.Subscribe<MonitoringMessage.GetAllPersistentSubscriptionStats>(persistentSubscription);
			perSubscrBus.Subscribe<MonitoringMessage.GetStreamPersistentSubscriptionStats>(persistentSubscription);
			perSubscrBus.Subscribe<MonitoringMessage.GetPersistentSubscriptionStats>(persistentSubscription);
			perSubscrBus.Subscribe<SubscriptionMessage.PersistentSubscriptionTimerTick>(persistentSubscription);
			perSubscrBus.Subscribe<SubscriptionMessage.PersistentSubscriptionsRestart>(persistentSubscription);

			// STORAGE SCAVENGER
			var scavengerLogManager = new TFChunkScavengerLogManager(NodeInfo.HttpEndPoint.ToString(),
				TimeSpan.FromDays(options.Database.ScavengeHistoryMaxAge), ioDispatcher);
			var storageScavenger = new StorageScavenger<TStreamId>(Db,
				tableIndex,
				readIndex,
				logFormat.SystemStreams,
				scavengerLogManager,
				options.Database.AlwaysKeepScavenged,
				!options.Database.DisableScavengeMerging,
				unsafeIgnoreHardDeletes: options.Database.UnsafeIgnoreHardDelete);

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

			var memberInfo = MemberInfo.Initial(NodeInfo.InstanceId, _timeProvider.UtcNow, VNodeState.Unknown, true,
				GossipAdvertiseInfo.InternalTcp,
				GossipAdvertiseInfo.InternalSecureTcp,
				GossipAdvertiseInfo.ExternalTcp,
				GossipAdvertiseInfo.ExternalSecureTcp,
				GossipAdvertiseInfo.HttpEndPoint,
				GossipAdvertiseInfo.AdvertiseHostToClientAs,
				GossipAdvertiseInfo.AdvertiseHttpPortToClientAs,
				GossipAdvertiseInfo.AdvertiseTcpPortToClientAs,
				options.Cluster.NodePriority, options.Cluster.ReadOnlyReplica);

			if (!isSingleNode) {
				// LEADER REPLICATION
				var leaderReplicationService = new LeaderReplicationService(_mainQueue, NodeInfo.InstanceId, Db,
					_workersHandler,
					epochManager, options.Cluster.ClusterSize,
					options.Cluster.UnsafeAllowSurplusNodes,
					_queueStatsManager);
				AddTask(leaderReplicationService.Task);
				_mainBus.Subscribe<SystemMessage.SystemStart>(leaderReplicationService);
				_mainBus.Subscribe<SystemMessage.StateChangeMessage>(leaderReplicationService);
				_mainBus.Subscribe<ReplicationMessage.ReplicaSubscriptionRequest>(leaderReplicationService);
				_mainBus.Subscribe<ReplicationMessage.ReplicaLogPositionAck>(leaderReplicationService);
				_mainBus.Subscribe<ReplicationTrackingMessage.ReplicatedTo>(leaderReplicationService);
				monitoringInnerBus.Subscribe<ReplicationMessage.GetReplicationStats>(leaderReplicationService);

				// REPLICA REPLICATION
				var replicaService = new ReplicaService(_mainQueue, Db, epochManager, _workersHandler,
					_authenticationProvider, AuthorizationGateway,
					GossipAdvertiseInfo.InternalTcp ?? GossipAdvertiseInfo.InternalSecureTcp,
					options.Cluster.ReadOnlyReplica,
					!disableInternalTcpTls, _internalServerCertificateValidator,
					_certificateSelector,
					TimeSpan.FromMilliseconds(options.Interface.IntTcpHeartbeatTimeout),
					TimeSpan.FromMilliseconds(options.Interface.ExtTcpHeartbeatInterval),
					TimeSpan.FromMilliseconds(options.Database.WriteTimeoutMs));
				_mainBus.Subscribe<SystemMessage.StateChangeMessage>(replicaService);
				_mainBus.Subscribe<ReplicationMessage.ReconnectToLeader>(replicaService);
				_mainBus.Subscribe<ReplicationMessage.SubscribeToLeader>(replicaService);
				_mainBus.Subscribe<ReplicationMessage.AckLogPosition>(replicaService);
				_mainBus.Subscribe<ClientMessage.TcpForwardMessage>(replicaService);
			}

			// ELECTIONS
			if (!NodeInfo.IsReadOnlyReplica) {
				var electionsService = new ElectionsService(
					_mainQueue,
					memberInfo,
					options.Cluster.ClusterSize,
					Db.Config.WriterCheckpoint.AsReadOnly(),
					Db.Config.ChaserCheckpoint.AsReadOnly(),
					Db.Config.ProposalCheckpoint,
					epochManager,
					() => readIndex.LastIndexedPosition,
					options.Cluster.NodePriority,
					_timeProvider);
				electionsService.SubscribeMessages(_mainBus);
			}

			if (!isSingleNode || (options.Interface.GossipOnSingleNode ?? true)) {
				// GOSSIP

				var gossipSeedSource = (
					options.Cluster.DiscoverViaDns,
					options.Cluster.ClusterSize > 1,
					options.Cluster.GossipSeed is {Length: >0}) switch {
					(true, true, _) => (IGossipSeedSource)new DnsGossipSeedSource(options.Cluster.ClusterDns,
						options.Cluster.ClusterGossipPort),
					(false, true, false) => throw new InvalidConfigurationException(
						"DNS discovery is disabled, but no gossip seed endpoints have been specified. "
						+ "Specify gossip seeds using the `GossipSeed` option."),
					_ => new KnownEndpointGossipSeedSource(options.Cluster.GossipSeed)
				};

				var gossip = new NodeGossipService(
					_mainQueue,
					gossipSeedSource,
					memberInfo,
					Db.Config.WriterCheckpoint.AsReadOnly(),
					Db.Config.ChaserCheckpoint.AsReadOnly(),
					epochManager, () => readIndex.LastIndexedPosition,
					options.Cluster.NodePriority, TimeSpan.FromMilliseconds(options.Cluster.GossipIntervalMs),
					TimeSpan.FromMilliseconds(options.Cluster.GossipAllowedDifferenceMs),
					TimeSpan.FromMilliseconds(options.Cluster.GossipTimeoutMs),
					TimeSpan.FromSeconds(options.Cluster.DeadMemberRemovalPeriodSec),
					_timeProvider);
				_mainBus.Subscribe<SystemMessage.SystemInit>(gossip);
				_mainBus.Subscribe<GossipMessage.RetrieveGossipSeedSources>(gossip);
				_mainBus.Subscribe<GossipMessage.GotGossipSeedSources>(gossip);
				_mainBus.Subscribe<GossipMessage.Gossip>(gossip);
				_mainBus.Subscribe<GossipMessage.GossipReceived>(gossip);
				_mainBus.Subscribe<GossipMessage.ReadGossip>(gossip);
				_mainBus.Subscribe<GossipMessage.ClientGossip>(gossip);
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

			if (Runtime.IsUnixOrMac) {
				UnixSignalManager.GetInstance().Subscribe(Signum.SIGHUP, () => {
					Log.Information("Reloading the node's configuration since the SIGHUP signal has been received.");
					_mainQueue.Publish(new ClientMessage.ReloadConfig());
				});
			}

			if (_subsystems != null) {
				foreach (var subsystem in _subsystems) {
					var http = new[] { _httpService };
					subsystem.Register(new StandardComponents(Db, _mainQueue, _mainBus, _timerService, _timeProvider,
						httpSendService, http, _workersHandler, _queueStatsManager));
				}
			}

			_startup = new ClusterVNodeStartup<TStreamId>(_subsystems, _mainQueue, _mainBus, _workersHandler,
				_authenticationProvider, httpAuthenticationProviders, _authorizationProvider, _readIndex,
				options.Application.MaxAppendSize, _httpService);
			_mainBus.Subscribe<SystemMessage.SystemReady>(_startup);
			_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(_startup);
		}

		private void SubscribeWorkers(Action<InMemoryBus> setup) {
			foreach (var workerBus in _workerBuses) {
				setup(workerBus);
			}
		}

		public override void Start() {
			_mainQueue.Publish(new SystemMessage.SystemInit());
		}

		public override async Task StopAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default) {
			if (Interlocked.Exchange(ref _stopCalled, 1) == 1) {
				Log.Warning("Stop was already called.");
				return;
			}

			timeout ??= TimeSpan.FromSeconds(5);
			_mainQueue.Publish(new ClientMessage.RequestShutdown(false, true));

			UnixSignalManager.StopProcessing();

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
			UnixSignalManager.StopProcessing();

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

		public override async Task<ClusterVNode> StartAsync(bool waitUntilReady) {
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
			X509Chain chain, SslPolicyErrors sslPolicyErrors, Func<X509Certificate2Collection> trustedRootCertsSelector) {
			return ValidateCertificateWithTrustedRootCerts(certificate, chain, sslPolicyErrors, trustedRootCertsSelector, "server");
		}

		public static ValueTuple<bool, string> ValidateClientCertificateWithTrustedRootCerts(X509Certificate certificate,
			X509Chain chain, SslPolicyErrors sslPolicyErrors, Func<X509Certificate2Collection> trustedRootCertsSelector) {
			return ValidateCertificateWithTrustedRootCerts(certificate, chain, sslPolicyErrors, trustedRootCertsSelector, "client");
		}

		private static ValueTuple<bool, string> ValidateCertificateWithTrustedRootCerts(X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors, Func<X509Certificate2Collection> trustedRootCertsSelector, string certificateOrigin) {
			if (certificate == null)
				return (false, $"No certificate was provided by the {certificateOrigin}");

			var newChain = new X509Chain {
				ChainPolicy = {
					RevocationMode = X509RevocationMode.NoCheck
				}
			};

			var trustedRootCerts = trustedRootCertsSelector();
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
				return (false, $"The certificate ({certificate.Subject}) provided by the {certificateOrigin} failed validation with the following error(s): {sslPolicyErrors.ToString()} ({chainStatus})");
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

		public void Handle(ClientMessage.ReloadConfig message) {
			if (Interlocked.CompareExchange(ref _reloadingConfig, 1, 0) != 0) {
				Log.Information("The node's configuration reload is already in progress");
				return;
			}

			Task.Run(() => {
				try {
					var options = _options.Reload();
					ReloadLogOptions(options);
					ReloadCertificates(options);
					Log.Information("The node's configuration was successfully reloaded");
				} catch (Exception exc) {
					Log.Error(exc, "An error has occurred while reloading the configuration");
				} finally {
					Interlocked.Exchange(ref _reloadingConfig, 0);
				}
			});
		}

		private void ReloadLogOptions(ClusterVNodeOptions options) {
			if (options.Application.LogLevel != LogLevel.Default) {
				var changed = EventStoreLoggerConfiguration.AdjustMinimumLogLevel(options.Application.LogLevel);
				if (changed) {
					Log.Information($"The log level was adjusted to: {options.Application.LogLevel}");

					if (options.Application.LogLevel > LogLevel.Information) {
						Console.WriteLine($"The log level was adjusted to: {options.Application.LogLevel}");
					}
				}
			}
		}

		private void ReloadCertificates(ClusterVNodeOptions options) {
			if (options.Application.Insecure) {
				Log.Information("Skipping reload of certificates since TLS is disabled.");
				return;
			}

			var certificate = options.LoadServerCertificate();

			var previousThumbprint = _certificate?.Thumbprint;
			var newThumbprint = certificate.Thumbprint;

			//no need for a lock here since reference assignment is atomic
			_certificate = certificate;
			_trustedRootCerts = options.LoadTrustedRootCertificates();

			Log.Information("Certificate loaded. Previous thumbprint: {previousThumbprint}, New thumbprint: {newThumbprint}", previousThumbprint, newThumbprint);
		}

		public override string ToString() =>
			$"[{NodeInfo.InstanceId:B}, {NodeInfo.InternalTcp}, {NodeInfo.ExternalTcp}, {NodeInfo.HttpEndPoint}]";
	}
}
