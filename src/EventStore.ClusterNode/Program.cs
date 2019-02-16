using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using EventStore.Common.Exceptions;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core;
using EventStore.Core.Authentication;
using EventStore.Core.PluginModel;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Util;
using System.Net.NetworkInformation;
using EventStore.Core.Data;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;

namespace EventStore.ClusterNode {
	public class Program : ProgramBase<ClusterNodeOptions> {
		private ClusterVNode _node;
		private ExclusiveDbLock _dbLock;
		private ClusterNodeMutex _clusterNodeMutex;

		public static void Main(string[] args) {
			Console.CancelKeyPress += delegate { Environment.Exit((int)ExitCode.Success); };
			var p = new Program();
			p.Run(args);
		}

		protected override string GetLogsDirectory(ClusterNodeOptions options) {
			return options.Log;
		}

		protected override string GetComponentName(ClusterNodeOptions options) {
			return string.Format("{0}-{1}-cluster-node", options.ExtIp, options.ExtHttpPort);
		}

		protected override void PreInit(ClusterNodeOptions options) {
			base.PreInit(options);

			if (options.Db.StartsWith("~") && !options.Force) {
				throw new ApplicationInitializationException(
					"The given database path starts with a '~'. We don't expand '~'. You can use --force to override this error.");
			}

			if (options.Log.StartsWith("~") && !options.Force) {
				throw new ApplicationInitializationException(
					"The given log path starts with a '~'. We don't expand '~'. You can use --force to override this error.");
			}

			if (options.GossipSeed.Length > 1 && options.ClusterSize == 1) {
				throw new ApplicationInitializationException(
					"The given ClusterSize is set to 1 but GossipSeeds are multiple. We will never be able to sync up with this configuration.");
			}

			//Never seen this problem occur on the .NET framework
			if (!Runtime.IsMono)
				return;

			//0 indicates we should leave the machine defaults alone
			if (options.MonoMinThreadpoolSize == 0)
				return;

			//Change the number of worker threads to be higher if our own setting
			// is higher than the current value
			int minWorkerThreads, minIocpThreads;
			ThreadPool.GetMinThreads(out minWorkerThreads, out minIocpThreads);

			if (minWorkerThreads >= options.MonoMinThreadpoolSize)
				return;

			if (!ThreadPool.SetMinThreads(options.MonoMinThreadpoolSize, minIocpThreads))
				Log.Error(
					"Cannot override the minimum number of Threadpool threads (machine default: {minWorkerThreads}, specified value: {monoMinThreadpoolSize})",
					minWorkerThreads, options.MonoMinThreadpoolSize);
		}

		protected override void Create(ClusterNodeOptions opts) {
			var dbPath = opts.Db;

			if (!opts.MemDb) {
				_dbLock = new ExclusiveDbLock(dbPath);
				if (!_dbLock.Acquire())
					throw new Exception(string.Format("Couldn't acquire exclusive lock on DB at '{0}'.", dbPath));
			}

			_clusterNodeMutex = new ClusterNodeMutex();
			if (!_clusterNodeMutex.Acquire())
				throw new Exception(string.Format("Couldn't acquire exclusive Cluster Node mutex '{0}'.",
					_clusterNodeMutex.MutexName));

			if (!opts.DiscoverViaDns && opts.GossipSeed.Length == 0) {
				if (opts.ClusterSize == 1) {
					Log.Info("DNS discovery is disabled, but no gossip seed endpoints have been specified. Since "
					         + "the cluster size is set to 1, this may be intentional. Gossip seeds can be specified "
					         + "using the `GossipSeed` option.");
				}
			}

			var runProjections = opts.RunProjections;
			var enabledNodeSubsystems = runProjections >= ProjectionType.System
				? new[] {NodeSubsystems.Projections}
				: new NodeSubsystems[0];
			_node = BuildNode(opts);
			RegisterWebControllers(enabledNodeSubsystems, opts);
		}

		private void RegisterWebControllers(NodeSubsystems[] enabledNodeSubsystems, ClusterNodeOptions options) {
			if (_node.InternalHttpService != null) {
				_node.InternalHttpService.SetupController(new ClusterWebUiController(_node.MainQueue,
					enabledNodeSubsystems));
			}

			if (options.AdminOnExt) {
				_node.ExternalHttpService.SetupController(new ClusterWebUiController(_node.MainQueue,
					enabledNodeSubsystems));
			}
		}

		private static int GetQuorumSize(int clusterSize) {
			if (clusterSize == 1) return 1;
			return clusterSize / 2 + 1;
		}

		private static ClusterVNode BuildNode(ClusterNodeOptions options) {
			var quorumSize = GetQuorumSize(options.ClusterSize);

			var intHttp = new IPEndPoint(options.IntIp, options.IntHttpPort);
			var extHttp = new IPEndPoint(options.ExtIp, options.ExtHttpPort);
			var intTcp = new IPEndPoint(options.IntIp, options.IntTcpPort);
			var intSecTcp = options.IntSecureTcpPort > 0
				? new IPEndPoint(options.IntIp, options.IntSecureTcpPort)
				: null;
			var extTcp = new IPEndPoint(options.ExtIp, options.ExtTcpPort);
			var extSecTcp = options.ExtSecureTcpPort > 0
				? new IPEndPoint(options.ExtIp, options.ExtSecureTcpPort)
				: null;

			var prepareCount = options.PrepareCount > quorumSize ? options.PrepareCount : quorumSize;
			var commitCount = options.CommitCount > quorumSize ? options.CommitCount : quorumSize;
			Log.Info("Quorum size set to {quorum}", prepareCount);
			if (options.DisableInsecureTCP) {
				if (!options.UseInternalSsl) {
					throw new Exception(
						"You have chosen to disable the insecure TCP ports and haven't set 'UseInternalSsl'. The nodes in the cluster will not be able to communicate properly.");
				}

				if (extSecTcp == null || intSecTcp == null) {
					throw new Exception(
						"You have chosen to disable the insecure TCP ports and haven't setup the External or Internal Secure TCP Ports.");
				}
			}

			if (options.UseInternalSsl) {
				if (ReferenceEquals(options.SslTargetHost, Opts.SslTargetHostDefault))
					throw new Exception("No SSL target host specified.");
				if (intSecTcp == null)
					throw new Exception(
						"Usage of internal secure communication is specified, but no internal secure endpoint is specified!");
			}

			VNodeBuilder builder;
			if (options.ClusterSize > 1) {
				builder = ClusterVNodeBuilder.AsClusterMember(options.ClusterSize);
			} else {
				builder = ClusterVNodeBuilder.AsSingleNode();
			}

			if (options.MemDb) {
				builder = builder.RunInMemory();
			} else {
				builder = builder.RunOnDisk(options.Db);
			}

			builder.WithInternalTcpOn(intTcp)
				.WithInternalSecureTcpOn(intSecTcp)
				.WithExternalTcpOn(extTcp)
				.WithExternalSecureTcpOn(extSecTcp)
				.WithInternalHttpOn(intHttp)
				.WithExternalHttpOn(extHttp)
				.WithWorkerThreads(options.WorkerThreads)
				.WithInternalHeartbeatTimeout(TimeSpan.FromMilliseconds(options.IntTcpHeartbeatTimeout))
				.WithInternalHeartbeatInterval(TimeSpan.FromMilliseconds(options.IntTcpHeartbeatInterval))
				.WithExternalHeartbeatTimeout(TimeSpan.FromMilliseconds(options.ExtTcpHeartbeatTimeout))
				.WithExternalHeartbeatInterval(TimeSpan.FromMilliseconds(options.ExtTcpHeartbeatInterval))
				.MaximumMemoryTableSizeOf(options.MaxMemTableSize)
				.WithHashCollisionReadLimitOf(options.HashCollisionReadLimit)
				.WithGossipInterval(TimeSpan.FromMilliseconds(options.GossipIntervalMs))
				.WithGossipAllowedTimeDifference(TimeSpan.FromMilliseconds(options.GossipAllowedDifferenceMs))
				.WithGossipTimeout(TimeSpan.FromMilliseconds(options.GossipTimeoutMs))
				.WithClusterGossipPort(options.ClusterGossipPort)
				.WithMinFlushDelay(TimeSpan.FromMilliseconds(options.MinFlushDelayMs))
				.WithPrepareTimeout(TimeSpan.FromMilliseconds(options.PrepareTimeoutMs))
				.WithCommitTimeout(TimeSpan.FromMilliseconds(options.CommitTimeoutMs))
				.WithStatsPeriod(TimeSpan.FromSeconds(options.StatsPeriodSec))
				.WithPrepareCount(prepareCount)
				.WithCommitCount(commitCount)
				.WithNodePriority(options.NodePriority)
				.WithScavengeHistoryMaxAge(options.ScavengeHistoryMaxAge)
				.WithIndexPath(options.Index)
				.WithIndexVerification(options.SkipIndexVerify)
				.WithIndexCacheDepth(options.IndexCacheDepth)
				.WithIndexMergeOptimization(options.OptimizeIndexMerge)
				.WithSslTargetHost(options.SslTargetHost)
				.RunProjections(options.RunProjections, options.ProjectionThreads, options.FaultOutOfOrderProjections)
				.WithProjectionQueryExpirationOf(TimeSpan.FromMinutes(options.ProjectionsQueryExpiry))
				.WithTfCachedChunks(options.CachedChunks)
				.WithTfChunksCacheSize(options.ChunksCacheSize)
				.WithStatsStorage(StatsStorage.StreamAndFile)
				.AdvertiseInternalIPAs(options.IntIpAdvertiseAs)
				.AdvertiseExternalIPAs(options.ExtIpAdvertiseAs)
				.AdvertiseInternalHttpPortAs(options.IntHttpPortAdvertiseAs)
				.AdvertiseExternalHttpPortAs(options.ExtHttpPortAdvertiseAs)
				.AdvertiseInternalTCPPortAs(options.IntTcpPortAdvertiseAs)
				.AdvertiseExternalTCPPortAs(options.ExtTcpPortAdvertiseAs)
				.AdvertiseInternalSecureTCPPortAs(options.IntSecureTcpPortAdvertiseAs)
				.AdvertiseExternalSecureTCPPortAs(options.ExtSecureTcpPortAdvertiseAs)
				.HavingReaderThreads(options.ReaderThreadsCount)
				.WithConnectionPendingSendBytesThreshold(options.ConnectionPendingSendBytesThreshold)
				.WithChunkInitialReaderCount(options.ChunkInitialReaderCount)
				.WithInitializationThreads(options.InitializationThreads)
				.WithMaxAutoMergeIndexLevel(options.MaxAutoMergeIndexLevel);

			if (options.GossipSeed.Length > 0)
				builder.WithGossipSeeds(options.GossipSeed);

			if (options.DiscoverViaDns)
				builder.WithClusterDnsName(options.ClusterDns);
			else
				builder.DisableDnsDiscovery();

			if (!options.AddInterfacePrefixes) {
				builder.DontAddInterfacePrefixes();
			}

			if (options.GossipOnSingleNode) {
				builder.GossipAsSingleNode();
			}

			foreach (var prefix in options.IntHttpPrefixes) {
				builder.AddInternalHttpPrefix(prefix);
			}

			foreach (var prefix in options.ExtHttpPrefixes) {
				builder.AddExternalHttpPrefix(prefix);
			}

			if (options.EnableTrustedAuth)
				builder.EnableTrustedAuth();
			if (options.StartStandardProjections)
				builder.StartStandardProjections();
			if (options.DisableHTTPCaching)
				builder.DisableHTTPCaching();
			if (options.DisableScavengeMerging)
				builder.DisableScavengeMerging();
			if (options.LogHttpRequests)
				builder.EnableLoggingOfHttpRequests();
			if (options.EnableHistograms)
				builder.EnableHistograms();
			if (options.UnsafeIgnoreHardDelete)
				builder.WithUnsafeIgnoreHardDelete();
			if (options.UnsafeDisableFlushToDisk)
				builder.WithUnsafeDisableFlushToDisk();
			if (options.BetterOrdering)
				builder.WithBetterOrdering();
			if (options.SslValidateServer)
				builder.ValidateSslServer();
			if (options.UseInternalSsl)
				builder.EnableSsl();
			if (options.DisableInsecureTCP)
				builder.DisableInsecureTCP();
			if (!options.AdminOnExt)
				builder.NoAdminOnPublicInterface();
			if (!options.StatsOnExt)
				builder.NoStatsOnPublicInterface();
			if (!options.GossipOnExt)
				builder.NoGossipOnPublicInterface();
			if (options.SkipDbVerify)
				builder.DoNotVerifyDbHashes();
			if (options.AlwaysKeepScavenged)
				builder.AlwaysKeepScavenged();
			if (options.Unbuffered)
				builder.EnableUnbuffered();
			if (options.WriteThrough)
				builder.EnableWriteThrough();
			if (options.SkipIndexScanOnReads)
				builder.SkipIndexScanOnReads();
			if (options.ReduceFileCachePressure)
				builder.ReduceFileCachePressure();
			if (options.StructuredLog)
				builder.WithStructuredLogging(options.StructuredLog);

			if (options.IntSecureTcpPort > 0 || options.ExtSecureTcpPort > 0) {
				if (!string.IsNullOrWhiteSpace(options.CertificateStoreLocation)) {
					var location = GetCertificateStoreLocation(options.CertificateStoreLocation);
					var name = GetCertificateStoreName(options.CertificateStoreName);
					builder.WithServerCertificateFromStore(location, name, options.CertificateSubjectName,
						options.CertificateThumbprint);
				} else if (!string.IsNullOrWhiteSpace(options.CertificateStoreName)) {
					var name = GetCertificateStoreName(options.CertificateStoreName);
					builder.WithServerCertificateFromStore(name, options.CertificateSubjectName,
						options.CertificateThumbprint);
				} else if (options.CertificateFile.IsNotEmptyString()) {
					builder.WithServerCertificateFromFile(options.CertificateFile, options.CertificatePassword);
				} else
					throw new Exception("No server certificate specified.");
			}

			var authenticationConfig = String.IsNullOrEmpty(options.AuthenticationConfig)
				? options.Config
				: options.AuthenticationConfig;
			var plugInContainer = FindPlugins();
			var authenticationProviderFactory =
				GetAuthenticationProviderFactory(options.AuthenticationType, authenticationConfig, plugInContainer);
			var consumerStrategyFactories = GetPlugInConsumerStrategyFactories(plugInContainer);
			builder.WithAuthenticationProvider(authenticationProviderFactory);

			return builder.Build(options, consumerStrategyFactories);
		}

		private static IPersistentSubscriptionConsumerStrategyFactory[] GetPlugInConsumerStrategyFactories(
			CompositionContainer plugInContainer) {
			var allPlugins = plugInContainer.GetExports<IPersistentSubscriptionConsumerStrategyPlugin>();

			var strategyFactories = new List<IPersistentSubscriptionConsumerStrategyFactory>();

			foreach (var potentialPlugin in allPlugins) {
				try {
					var plugin = potentialPlugin.Value;
					Log.Info("Loaded consumer strategy plugin: {plugin} version {version}.", plugin.Name,
						plugin.Version);
					strategyFactories.Add(plugin.GetConsumerStrategyFactory());
				} catch (CompositionException ex) {
					Log.ErrorException(ex, "Error loading consumer strategy plugin.");
				}
			}

			return strategyFactories.ToArray();
		}

		private static IAuthenticationProviderFactory GetAuthenticationProviderFactory(string authenticationType,
			string authenticationConfigFile, CompositionContainer plugInContainer) {
			var potentialPlugins = plugInContainer.GetExports<IAuthenticationPlugin>();

			var authenticationTypeToPlugin = new Dictionary<string, Func<IAuthenticationProviderFactory>> {
				{"internal", () => new InternalAuthenticationProviderFactory()}
			};

			foreach (var potentialPlugin in potentialPlugins) {
				try {
					var plugin = potentialPlugin.Value;
					var commandLine = plugin.CommandLineName.ToLowerInvariant();
					Log.Info("Loaded authentication plugin: {plugin} version {version} (Command Line: {commandLine})",
						plugin.Name, plugin.Version, commandLine);
					authenticationTypeToPlugin.Add(commandLine,
						() => plugin.GetAuthenticationProviderFactory(authenticationConfigFile));
				} catch (CompositionException ex) {
					Log.ErrorException(ex, "Error loading authentication plugin.");
				}
			}

			Func<IAuthenticationProviderFactory> factory;
			if (!authenticationTypeToPlugin.TryGetValue(authenticationType.ToLowerInvariant(), out factory)) {
				throw new ApplicationInitializationException(string.Format(
					"The authentication type {0} is not recognised. If this is supposed " +
					"to be provided by an authentication plugin, confirm the plugin DLL is located in {1}.\n" +
					"Valid options for authentication are: {2}.", authenticationType, Locations.PluginsDirectory,
					string.Join(", ", authenticationTypeToPlugin.Keys)));
			}

			return factory();
		}

		private static CompositionContainer FindPlugins() {
			var catalog = new AggregateCatalog();

			catalog.Catalogs.Add(new AssemblyCatalog(typeof(Program).Assembly));

			if (Directory.Exists(Locations.PluginsDirectory)) {
				Log.Info("Plugins path: {pluginsDirectory}", Locations.PluginsDirectory);
				catalog.Catalogs.Add(new DirectoryCatalog(Locations.PluginsDirectory));
			} else {
				Log.Info("Cannot find plugins path: {pluginsDirectory}", Locations.PluginsDirectory);
			}

			return new CompositionContainer(catalog);
		}

		protected override void Start() {
			_node.Start();
		}

		public override void Stop() {
			_node.StopNonblocking(true, true);
		}

		protected override void OnProgramExit() {
			base.OnProgramExit();

			if (_dbLock != null && _dbLock.IsAcquired)
				_dbLock.Release();
		}

		protected override bool GetIsStructuredLog(ClusterNodeOptions options) {
			return options.StructuredLog;
		}
	}
}
