using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
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
using System.Threading.Tasks;
using EventStore.Core.Services;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;
using EventStore.Rags;

namespace EventStore.ClusterNode {
	internal class ClusterVNodeHostedService : EventStoreHostedService<ClusterNodeOptions> {
		private ExclusiveDbLock _dbLock;
		private ClusterNodeMutex _clusterNodeMutex;

		public ClusterVNode Node { get; private set; }

		public ClusterVNodeHostedService(string[] args) : base(args) {
		}

		protected override IEnumerable<OptionSource>
			MutateEffectiveOptions(IEnumerable<OptionSource> effectiveOptions) {
			var developmentOption = effectiveOptions.Single(x => x.Name == nameof(ClusterNodeOptions.Dev));
			bool.TryParse(developmentOption.Value.ToString(), out bool developmentMode);
			return effectiveOptions.Select(x => {
				if (x.Name == nameof(ClusterNodeOptions.MemDb)
				    && x.Source == "<DEFAULT>"
				    && developmentMode) {
					x.Value = true;
					x.Source = "Set by 'Development Mode' mode";
				}

				if (x.Name == nameof(ClusterNodeOptions.CertificatePassword)
				    && x.Source == "<DEFAULT>"
				    && developmentMode) {
					x.Value = SystemUsers.DefaultAdminPassword;
					x.Source = "Set by 'Development Mode' mode";
				}

				if (x.Name == nameof(ClusterNodeOptions.CertificateFile)
				    && x.Source == "<DEFAULT>"
				    && developmentMode) {
					x.Value = Path.Combine(Locations.DevCertificateDirectory, "server1.pfx");
					x.Source = "Set by 'Development Mode' mode";
				}

				return x;
			});
		}

		protected override string GetLogsDirectory(ClusterNodeOptions options) => options.Log;

		protected override string GetComponentName(ClusterNodeOptions options) =>
			$"{options.ExtIp}-{options.ExtHttpPort}-cluster-node";

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
		}

		protected override void Create(ClusterNodeOptions opts) {
			var dbPath = opts.Db;

			if (opts.Dev) {
				Log.Warning(
					"\n========================================================================================================\n" +
					"DEVELOPMENT MODE IS ON. THIS MODE IS *NOT* INTENDED FOR PRODUCTION USE.\n" +
					"WHEN IN DEVELOPMENT MODE EVENT STORE WILL\n" +
					" - NOT WRITE ANY DATA TO DISK.\n" +
					" - USE A SELF SIGNED CERTIFICATE.\n" +
					"========================================================================================================\n");
			}

			Log.Information(
				"\nINTERFACES\n" +
				"External TCP (Protobuf)\n" +
				$"\tEnabled\t: {opts.EnableExternalTCP}\n" +
				$"\tPort\t: {(opts.ExtSecureTcpPort > 0 ? opts.ExtSecureTcpPort : opts.ExtTcpPort)}\n" +
				"External HTTP (AtomPub)\n" +
				$"\tEnabled\t: {opts.EnableAtomPubOverHTTP}\n" +
				$"\tPort\t: {opts.ExtHttpPort}\n");

			if (opts.EnableAtomPubOverHTTP) {
				Log.Warning("\n DEPRECATION WARNING: AtomPub over HTTP Interface has been deprecated as of version 20.02. It is recommended to use gRPC instead.\n");
			}

			if (opts.EnableExternalTCP) {
				Log.Warning(
					"\n DEPRECATION WARNING: The Legacy TCP Client Interface has been deprecated as of version 20.02. "
					+ $"The External TCP Interface can be re-enabled with the '{nameof(Options.EnableExternalTCP)}' option. "
					+ "It is recommended to use gRPC instead.\n");
			}

			if (!opts.MemDb) {
				var absolutePath = Path.GetFullPath(dbPath);
				if (Runtime.IsWindows)
					absolutePath = absolutePath.ToLower();

				_dbLock = new ExclusiveDbLock(absolutePath);
				if (!_dbLock.Acquire())
					throw new Exception($"Couldn't acquire exclusive lock on DB at '{dbPath}'.");
			}

			_clusterNodeMutex = new ClusterNodeMutex();
			if (!_clusterNodeMutex.Acquire())
				throw new Exception($"Couldn't acquire exclusive Cluster Node mutex '{_clusterNodeMutex.MutexName}'.");

			if (!opts.DiscoverViaDns && opts.GossipSeed.Length == 0) {
				if (opts.ClusterSize == 1) {
					Log.Information(
						"DNS discovery is disabled, but no gossip seed endpoints have been specified. Since "
						+ "the cluster size is set to 1, this may be intentional. Gossip seeds can be specified "
						+ "using the `GossipSeed` option.");
				}
			}

			var runProjections = opts.RunProjections;
			var enabledNodeSubsystems = runProjections >= ProjectionType.System
				? new[] {NodeSubsystems.Projections}
				: new NodeSubsystems[0];
			Node = BuildNode(opts);

			RegisterWebControllers(enabledNodeSubsystems, opts);
		}

		private void RegisterWebControllers(NodeSubsystems[] enabledNodeSubsystems, ClusterNodeOptions options) {
			if (options.AdminOnExt) {
				Node.ExternalHttpService.SetupController(new ClusterWebUiController(Node.MainQueue,
					enabledNodeSubsystems));
			}
		}

		private static int GetQuorumSize(int clusterSize) {
			if (clusterSize == 1)
				return 1;
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
			Log.Information("Quorum size set to {quorum}", prepareCount);

			if (!options.DisableInternalTls) {
				if (ReferenceEquals(options.TlsTargetHost, Opts.TlsTargetHostDefault))
					throw new Exception("No TLS target host specified.");
				if (intSecTcp == null)
					throw new Exception(
						"Usage of internal secure communication is specified, but no internal secure endpoint is specified!");
			}

			if (options.ReadOnlyReplica && options.ClusterSize <= 1) {
				throw new Exception(
					"This node cannot be configured as a Read Only Replica as these node types are only supported in a clustered configuration.");
			}

			VNodeBuilder builder;
			if (options.ClusterSize > 1) {
				builder = ClusterVNodeBuilder.AsClusterMember(options.ClusterSize);
				if (options.ReadOnlyReplica)
					builder.EnableReadOnlyReplica();
			} else {
				builder = ClusterVNodeBuilder.AsSingleNode();
			}

			if (options.MemDb) {
				builder = builder.RunInMemory();
			} else {
				builder = builder.RunOnDisk(options.Db);
			}

			if (options.WriteStatsToDb) {
				builder = builder.WithStatsStorage(StatsStorage.StreamAndFile);
			} else {
				builder = builder.WithStatsStorage(StatsStorage.File);
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
				.WithDeadMemberRemovalPeriod(TimeSpan.FromSeconds(options.DeadMemberRemovalPeriodSec))
				.WithPrepareCount(prepareCount)
				.WithCommitCount(commitCount)
				.WithNodePriority(options.NodePriority)
				.WithScavengeHistoryMaxAge(options.ScavengeHistoryMaxAge)
				.WithIndexPath(options.Index)
				.WithIndexVerification(options.SkipIndexVerify)
				.WithIndexCacheDepth(options.IndexCacheDepth)
				.WithIndexMergeOptimization(options.OptimizeIndexMerge)
				.WithTlsTargetHost(options.TlsTargetHost)
				.RunProjections(options.RunProjections, options.ProjectionThreads, options.FaultOutOfOrderProjections)
				.WithProjectionQueryExpirationOf(TimeSpan.FromMinutes(options.ProjectionsQueryExpiry))
				.WithTfCachedChunks(options.CachedChunks)
				.WithTfChunksCacheSize(options.ChunksCacheSize)
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
				.WithConnectionQueueSizeThreshold(options.ConnectionQueueSizeThreshold)
				.WithChunkInitialReaderCount(options.ChunkInitialReaderCount)
				.WithInitializationThreads(options.InitializationThreads)
				.WithMaxAutoMergeIndexLevel(options.MaxAutoMergeIndexLevel)
				.WithMaxAppendSize(options.MaxAppendSize)
				.WithEnableAtomPubOverHTTP(options.EnableAtomPubOverHTTP);

			if (options.GossipSeed.Length > 0)
				builder.WithGossipSeeds(options.GossipSeed);

			if (options.DiscoverViaDns)
				builder.WithClusterDnsName(options.ClusterDns);
			else
				builder.DisableDnsDiscovery();

			if (options.GossipOnSingleNode) {
				builder.GossipAsSingleNode();
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
			if (options.LogFailedAuthenticationAttempts)
				builder.EnableLoggingOfFailedAuthenticationAttempts();
			if (options.EnableHistograms)
				builder.EnableHistograms();
			if (options.UnsafeIgnoreHardDelete)
				builder.WithUnsafeIgnoreHardDelete();
			if (options.UnsafeDisableFlushToDisk)
				builder.WithUnsafeDisableFlushToDisk();
			if (options.BetterOrdering)
				builder.WithBetterOrdering();
			if (options.DisableInternalTls)
				builder.DisableInternalTls();
			if (options.DisableExternalTls)
				builder.DisableExternalTls();
			if (options.EnableExternalTCP)
				builder.EnableExternalTCP();
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
			if (options.DisableFirstLevelHttpAuthorization)
				builder.DisableFirstLevelHttpAuthorization();
			if (options.UnsafeAllowSurplusNodes)
				builder.WithUnsafeAllowSurplusNodes();
			if (options.Dev)
				builder.WithHttpMessageHandlerFactory(() => new SocketsHttpHandler {
					SslOptions = new SslClientAuthenticationOptions {
						RemoteCertificateValidationCallback = delegate { return true; }
					}
				});

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
			} else if (!options.Dev)
				throw new Exception("An SSL Certificate is required unless development mode (--dev) is set.");

			var authenticationConfig = String.IsNullOrEmpty(options.AuthenticationConfig)
				? options.Config
				: options.AuthenticationConfig;
			var plugInContainer = FindPlugins();
			var authenticationProviderFactory =
				GetAuthenticationProviderFactory(options.AuthenticationType, authenticationConfig, plugInContainer);
			var consumerStrategyFactories = GetPlugInConsumerStrategyFactories(plugInContainer);
			builder.WithAuthenticationProvider(authenticationProviderFactory);
			var subsystemFactories = GetPlugInSubsystemFactories(plugInContainer);

			foreach (var subsystemFactory in subsystemFactories) {
				var subsystem = subsystemFactory.Create(options.Config);
				builder.AddCustomSubsystem(subsystem);
			}

			return builder.Build(options, consumerStrategyFactories);
		}

		private static IPersistentSubscriptionConsumerStrategyFactory[] GetPlugInConsumerStrategyFactories(
			CompositionContainer plugInContainer) {
			var allPlugins = plugInContainer.GetExports<IPersistentSubscriptionConsumerStrategyPlugin>();

			var strategyFactories = new List<IPersistentSubscriptionConsumerStrategyFactory>();

			foreach (var potentialPlugin in allPlugins) {
				try {
					var plugin = potentialPlugin.Value;
					Log.Information("Loaded consumer strategy plugin: {plugin} version {version}.", plugin.Name,
						plugin.Version);
					strategyFactories.Add(plugin.GetConsumerStrategyFactory());
				} catch (CompositionException ex) {
					Log.Error(ex, "Error loading consumer strategy plugin.");
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
					Log.Information(
						"Loaded authentication plugin: {plugin} version {version} (Command Line: {commandLine})",
						plugin.Name, plugin.Version, commandLine);
					authenticationTypeToPlugin.Add(commandLine,
						() => plugin.GetAuthenticationProviderFactory(authenticationConfigFile));
				} catch (CompositionException ex) {
					Log.Error(ex, "Error loading authentication plugin.");
				}
			}

			if (!authenticationTypeToPlugin.TryGetValue(authenticationType.ToLowerInvariant(), out var factory)) {
				throw new ApplicationInitializationException(
					$"The authentication type {authenticationType} is not recognised. If this is supposed " +
					$"to be provided by an authentication plugin, confirm the plugin DLL is located in {Locations.PluginsDirectory}." +
					Environment.NewLine +
					$"Valid options for authentication are: {string.Join(", ", authenticationTypeToPlugin.Keys)}.");
			}

			return factory();
		}

		private static ISubsystemFactory[] GetPlugInSubsystemFactories(
			CompositionContainer plugInContainer) {
			var allPlugins = plugInContainer.GetExports<ISubsystemPlugin>();

			var strategyFactories = new List<ISubsystemFactory>();

			foreach (var potentialPlugin in allPlugins) {
				try {
					var plugin = potentialPlugin.Value;
					Log.Information("Loaded subsystem plugin: {plugin} version {version}", plugin.Name, plugin.Version);
					strategyFactories.Add(plugin.GetSubsystemFactory());
				} catch (CompositionException ex) {
					Log.Error(ex, "Error loading subsystem plugin.");
				}
			}

			return strategyFactories.ToArray();
		}

		private static CompositionContainer FindPlugins() {
			var catalog = new AggregateCatalog();

			catalog.Catalogs.Add(new AssemblyCatalog(typeof(ClusterVNodeHostedService).Assembly));

			if (Directory.Exists(Locations.PluginsDirectory)) {
				Log.Information("Plugins path: {pluginsDirectory}", Locations.PluginsDirectory);

				Log.Information("Adding: {pluginsDirectory} to the plugin catalog.", Locations.PluginsDirectory);
				catalog.Catalogs.Add(new DirectoryCatalog(Locations.PluginsDirectory));

				foreach (string dirPath in Directory.GetDirectories(Locations.PluginsDirectory, "*",
					SearchOption.TopDirectoryOnly)) {
					Log.Information("Adding: {pluginsDirectory} to the plugin catalog.", dirPath);
					catalog.Catalogs.Add(new DirectoryCatalog(dirPath));
				}
			} else {
				Log.Information("Cannot find plugins path: {pluginsDirectory}", Locations.PluginsDirectory);
			}

			return new CompositionContainer(catalog);
		}

		protected override Task StartInternalAsync(CancellationToken cancellationToken) => Node.StartAsync(false);

		protected override Task StopInternalAsync(CancellationToken cancellationToken) {
			if (_dbLock != null && _dbLock.IsAcquired)
				_dbLock.Release();

			return Node.StopAsync(cancellationToken: cancellationToken);
		}
	}
}
