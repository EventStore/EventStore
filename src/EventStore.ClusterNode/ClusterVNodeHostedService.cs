using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using EventStore.Common.Exceptions;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core;
using EventStore.Core.Authentication;
using EventStore.Core.PluginModel;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Services.Transport.Http.Controllers;
using System.Threading.Tasks;
using EventStore.Core.Authentication.InternalAuthentication;
using EventStore.Core.Authorization;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;
using EventStore.Core.Util;
using EventStore.Rags;
using EventStore.Plugins.Authentication;
using EventStore.Plugins.Authorization;

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
			var insecureOption = effectiveOptions.Single(x => x.Name == nameof(ClusterNodeOptions.Insecure));
			bool.TryParse(developmentOption.Value.ToString(), out bool developmentMode);
			bool.TryParse(insecureOption.Value.ToString(), out bool insecureMode);
			return effectiveOptions.Select(x => {
				if (x.Name == nameof(ClusterNodeOptions.MemDb)
				    && x.Source == "<DEFAULT>"
				    && developmentMode) {
					x.Value = true;
					x.Source = "Set by 'Development' mode";
				}

				if (x.Name == nameof(ClusterNodeOptions.DisableInternalTcpTls)
				    && x.Source == "<DEFAULT>"
				    && insecureMode) {
					x.Value = true;
					x.Source =  "Set by 'Insecure' mode";
				}

				if (x.Name == nameof(ClusterNodeOptions.DisableExternalTcpTls)
				    && x.Source == "<DEFAULT>"
				    && insecureMode) {
					x.Value = true;
					x.Source =  "Set by 'Insecure' mode";
				}

				if (x.Name == nameof(ClusterNodeOptions.DisableHttps)
				    && x.Source == "<DEFAULT>"
				    && insecureMode) {
					x.Value = true;
					x.Source =  "Set by 'Insecure' mode";
				}

				if (x.Name == nameof(ClusterNodeOptions.CertificateFile)
				    && x.Source == "<DEFAULT>"
				    && developmentMode && !insecureMode) {
					x.Value = Path.Combine(Locations.DevCertificateDirectory, "server1.pem");
					x.Source = "Set by 'Development' mode";
				}

				if (x.Name == nameof(ClusterNodeOptions.CertificatePrivateKeyFile)
				    && x.Source == "<DEFAULT>"
				    && developmentMode && !insecureMode) {
					x.Value = Path.Combine(Locations.DevCertificateDirectory, "server1.key");
					x.Source = "Set by 'Development' mode";
				}

				if (x.Name == nameof(ClusterNodeOptions.TrustedRootCertificatesPath)
				    && x.Source == "<DEFAULT>"
				    && developmentMode && !insecureMode) {
					x.Value = Locations.DevCertificateDirectory;
					x.Source = "Set by 'Development' mode";
				}

				if (x.Name == nameof(ClusterNodeOptions.EnableAtomPubOverHTTP)
				    && x.Source == "<DEFAULT>"
				    && developmentMode) {
					x.Value = true;
					x.Source = "Set by 'Development' mode";
				}

				return x;
			});
		}

		protected override string GetLogsDirectory(ClusterNodeOptions options) => options.Log;

		protected override string GetComponentName(ClusterNodeOptions options) =>
			$"{options.ExtIp}-{options.HttpPort}-cluster-node";

		protected override void PreInit(ClusterNodeOptions options) {
			base.PreInit(options);

			if (options.Db.StartsWith("~")) {
				throw new ApplicationInitializationException(
					"The given database path starts with a '~'. Event Store does not expand '~'.");
			}

			if (options.Index != null && options.Db != null) {
				string absolutePathIndex = Path.GetFullPath(options.Index);
				string absolutePathDb = Path.GetFullPath(options.Db);
				if (absolutePathDb.Equals(absolutePathIndex)) {
					throw new ApplicationInitializationException(
						$"The given database ({absolutePathDb}) and index ({absolutePathIndex}) paths cannot point to the same directory.");
				}
			}

			if (options.Log.StartsWith("~")) {
				throw new ApplicationInitializationException(
					"The given log path starts with a '~'. Event Store does not expand '~'.");
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
				$"\tPort\t: {(opts.ExtTcpPort)}\n" +
				"HTTP (AtomPub)\n" +
				$"\tEnabled\t: {opts.EnableAtomPubOverHTTP}\n" +
				$"\tPort\t: {opts.HttpPort}\n");

			if (opts.EnableAtomPubOverHTTP) {
				Log.Warning(
					"\n DEPRECATION WARNING: AtomPub over HTTP Interface has been deprecated as of version 20.6.0. It is recommended to use gRPC instead.\n");
			}

			if (opts.EnableExternalTCP) {
				Log.Warning(
					"\n DEPRECATION WARNING: The Legacy TCP Client Interface has been deprecated as of version 20.6.0. "
					+ $"The External TCP Interface can be re-enabled with the '{nameof(Options.EnableExternalTCP)}' option. "
					+ "It is recommended to use gRPC instead.\n");
			}

			if (!opts.MemDb) {
				var absolutePath = Path.GetFullPath(dbPath);
				if (Runtime.IsWindows)
					absolutePath = absolutePath.ToLower();

				_dbLock = new ExclusiveDbLock(absolutePath);
				if (!_dbLock.Acquire())
					throw new InvalidConfigurationException($"Couldn't acquire exclusive lock on DB at '{dbPath}'.");
			}

			_clusterNodeMutex = new ClusterNodeMutex();
			if (!_clusterNodeMutex.Acquire())
				throw new InvalidConfigurationException($"Couldn't acquire exclusive Cluster Node mutex '{_clusterNodeMutex.MutexName}'.");

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
			if (!options.DisableAdminUi) {
				Node.HttpService.SetupController(new ClusterWebUiController(Node.MainQueue,
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

			var httpEndPoint = new IPEndPoint(options.ExtIp, options.HttpPort);
			var intTcp = options.DisableInternalTcpTls ? new IPEndPoint(options.IntIp, options.IntTcpPort) : null;
			var intSecTcp = !options.DisableInternalTcpTls ? new IPEndPoint(options.IntIp, options.IntTcpPort) : null;
			var extTcp = options.EnableExternalTCP && options.DisableExternalTcpTls
				? new IPEndPoint(options.ExtIp, options.ExtTcpPort)
				: null;
			var extSecTcp = options.EnableExternalTCP && !options.DisableExternalTcpTls
				? new IPEndPoint(options.ExtIp, options.ExtTcpPort)
				: null;

			var intTcpPortAdvertiseAs = options.DisableInternalTcpTls ? options.IntTcpPortAdvertiseAs : 0;
			var intSecTcpPortAdvertiseAs = !options.DisableInternalTcpTls ? options.IntTcpPortAdvertiseAs : 0;
			var extTcpPortAdvertiseAs = options.EnableExternalTCP && options.DisableExternalTcpTls
				? options.ExtTcpPortAdvertiseAs
				: 0;
			var extSecTcpPortAdvertiseAs = options.EnableExternalTCP && !options.DisableExternalTcpTls
				? options.ExtTcpPortAdvertiseAs
				: 0;

			var prepareCount = options.PrepareCount > quorumSize ? options.PrepareCount : quorumSize;
			var commitCount = options.CommitCount > quorumSize ? options.CommitCount : quorumSize;
			Log.Information("Quorum size set to {quorum}", prepareCount);

			if (options.ReadOnlyReplica && options.ClusterSize <= 1) {
				throw new InvalidConfigurationException(
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
				.WithHttpOn(httpEndPoint)
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
				.WithWriteTimeout(TimeSpan.FromMilliseconds(options.WriteTimeoutMs))
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
				.RunProjections(options.RunProjections, options.ProjectionThreads, options.FaultOutOfOrderProjections)
				.WithProjectionQueryExpirationOf(TimeSpan.FromMinutes(options.ProjectionsQueryExpiry))
				.WithTfCachedChunks(options.CachedChunks)
				.WithTfChunksCacheSize(options.ChunksCacheSize)
				.AdvertiseInternalHostAs(options.IntHostAdvertiseAs)
				.AdvertiseExternalHostAs(options.ExtHostAdvertiseAs)
				.AdvertiseHttpPortAs(options.HttpPortAdvertiseAs)
				.AdvertiseInternalTCPPortAs(intTcpPortAdvertiseAs)
				.AdvertiseExternalTCPPortAs(extTcpPortAdvertiseAs)
				.AdvertiseInternalSecureTCPPortAs(intSecTcpPortAdvertiseAs)
				.AdvertiseExternalSecureTCPPortAs(extSecTcpPortAdvertiseAs)
				.HavingReaderThreads(options.ReaderThreadsCount)
				.WithConnectionPendingSendBytesThreshold(options.ConnectionPendingSendBytesThreshold)
				.WithConnectionQueueSizeThreshold(options.ConnectionQueueSizeThreshold)
				.WithChunkInitialReaderCount(options.ChunkInitialReaderCount)
				.WithInitializationThreads(options.InitializationThreads)
				.WithMaxAutoMergeIndexLevel(options.MaxAutoMergeIndexLevel)
				.WithMaxTruncation(options.MaxTruncation)
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
			if (options.DisableInternalTcpTls)
				builder.DisableInternalTcpTls();
			if (options.DisableExternalTcpTls)
				builder.DisableExternalTcpTls();
			if (options.DisableHttps)
				builder.DisableHttps();
			if (options.EnableExternalTCP)
				builder.EnableExternalTCP();
			if (options.DisableAdminUi)
				builder.NoAdminOnPublicInterface();
			if (options.DisableStatsOnHttp)
				builder.NoStatsOnPublicInterface();
			if (options.DisableGossipOnHttp)
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
			
			builder.WithCertificateReservedNodeCommonName(options.CertificateReservedNodeCommonName);

			var requireCertHttp = !options.DisableHttps;
			var requireCertIntTcp = options.ClusterSize > 1 && !options.DisableInternalTcpTls;
			var requireCertExtTcp = options.EnableExternalTCP && !options.DisableExternalTcpTls;

			var message = "\nSECURITY\n";
			if (options.ClusterSize > 1) {
				message += "Internal TCP (Replication)\n" +  $"\tTLS enabled\t: {requireCertIntTcp}\n";
			}

			message += "HTTP (gRPC / Admin UI)\n" + $"\tTLS enabled\t: {requireCertHttp}\n";

			if (options.EnableExternalTCP) {
				message += "External TCP (Protobuf)\n" + $"\tTLS enabled\t: {requireCertExtTcp}\n";
			}

			bool requireCertificates = requireCertHttp || requireCertIntTcp || requireCertExtTcp;

			if (requireCertificates) {
				message += "\nTLS is enabled on at least one TCP/HTTP interface - a certificate is required to run EventStoreDB.";
			} else {
				message += "\nTLS is disabled on all TCP/HTTP interfaces - no certificates are required to run EventStoreDB.";
				message += "\nIt is recommended to run with TLS enabled in production.\n";
			}

			Log.Information(message);

			if (requireCertificates) {
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
					builder.WithServerCertificateFromFile(
						options.CertificateFile,
						options.CertificatePrivateKeyFile,
						options.CertificatePassword);
				} else if (!options.Dev)
					throw new InvalidConfigurationException(
						"A certificate is required unless development mode (--dev) is set to use development certificates or insecure mode (--insecure) is set to disable TLS on all TCP/HTTP interfaces.");

				if (!string.IsNullOrEmpty(options.TrustedRootCertificatesPath)) {
					builder.WithTrustedRootCertificatesPath(options.TrustedRootCertificatesPath);
				} else {
					throw new InvalidConfigurationException(
						$"{nameof(options.TrustedRootCertificatesPath)} must be specified unless development mode (--dev) is set to use development certificates or insecure mode (--insecure) is set to disable TLS on all TCP/HTTP interfaces.");
				}
			}

			var authorizationConfig = String.IsNullOrEmpty(options.AuthorizationConfig)
				? options.Config
				: options.AuthorizationConfig;

			var authenticationConfig = String.IsNullOrEmpty(options.AuthenticationConfig)
				? options.Config
				: options.AuthenticationConfig;

			Action<Policy> internalPolicyOverrides = (policy) => {
				if (!options.DisableHttps) return;

				policy.Clear(Operations.Node.Gossip.Update);
				policy.AllowAnonymous(Operations.Node.Gossip.Update);

				policy.Clear(Operations.Node.Elections.Prepare);
				policy.AllowAnonymous(Operations.Node.Elections.Prepare);

				policy.Clear(Operations.Node.Elections.PrepareOk);
				policy.AllowAnonymous(Operations.Node.Elections.PrepareOk);

				policy.Clear(Operations.Node.Elections.ViewChange);
				policy.AllowAnonymous(Operations.Node.Elections.ViewChange);

				policy.Clear(Operations.Node.Elections.ViewChangeProof);
				policy.AllowAnonymous(Operations.Node.Elections.ViewChangeProof);

				policy.Clear(Operations.Node.Elections.Proposal);
				policy.AllowAnonymous(Operations.Node.Elections.Proposal);

				policy.Clear(Operations.Node.Elections.Accept);
				policy.AllowAnonymous(Operations.Node.Elections.Accept);

				policy.Clear(Operations.Node.Elections.LeaderIsResigning);
				policy.AllowAnonymous(Operations.Node.Elections.LeaderIsResigning);

				policy.Clear(Operations.Node.Elections.LeaderIsResigningOk);
				policy.AllowAnonymous(Operations.Node.Elections.LeaderIsResigningOk);
			};

			var pluginLoader = new PluginLoader(new DirectoryInfo(Locations.PluginsDirectory));
			var authorizationProviderFactory =
				GetAuthorizationProviderFactory(options.AuthorizationType, authorizationConfig, pluginLoader, internalPolicyOverrides);
			var authenticationProviderFactory =
				GetAuthenticationProviderFactory(options.AuthenticationType, authenticationConfig, pluginLoader);

			var plugInContainer = FindPlugins();

			var consumerStrategyFactories = GetPlugInConsumerStrategyFactories(plugInContainer);
			builder.WithAuthenticationProviderFactory(authenticationProviderFactory,
				options.AuthenticationType == Opts.AuthenticationTypeDefault);
			builder.WithAuthorizationProvider(authorizationProviderFactory);
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

		private static AuthorizationProviderFactory GetAuthorizationProviderFactory(string authorizationType,
			string authorizationConfigFile, PluginLoader pluginLoader, Action<Policy> internalPolicyOverrides) {
			var authorizationTypeToPlugin = new Dictionary<string, AuthorizationProviderFactory> {
				{
					"internal", new AuthorizationProviderFactory(components =>
						new LegacyAuthorizationProviderFactory(components.MainQueue, internalPolicyOverrides))
				}
			};

			foreach (var potentialPlugin in pluginLoader.Load<IAuthorizationPlugin>()) {
				try {
					var commandLine = potentialPlugin.CommandLineName.ToLowerInvariant();
					Log.Information(
						"Loaded authorization plugin: {plugin} version {version} (Command Line: {commandLine})",
						potentialPlugin.Name, potentialPlugin.Version, commandLine);
					authorizationTypeToPlugin.Add(commandLine,
						new AuthorizationProviderFactory(_ =>
							potentialPlugin.GetAuthorizationProviderFactory(authorizationConfigFile)));
				} catch (CompositionException ex) {
					Log.Error(ex, "Error loading authentication plugin.");
				}
			}

			if (!authorizationTypeToPlugin.TryGetValue(authorizationType.ToLowerInvariant(), out var factory)) {
				throw new ApplicationInitializationException(
					$"The authorization type {authorizationType} is not recognised. If this is supposed " +
					$"to be provided by an authorization plugin, confirm the plugin DLL is located in {Locations.PluginsDirectory}." +
					Environment.NewLine +
					$"Valid options for authorization are: {string.Join(", ", authorizationTypeToPlugin.Keys)}.");
			}

			return factory;
		}

		private static AuthenticationProviderFactory GetAuthenticationProviderFactory(string authenticationType,
			string authenticationConfigFile, PluginLoader pluginLoader) {
			var authenticationTypeToPlugin = new Dictionary<string, AuthenticationProviderFactory> {
				{
					"internal", new AuthenticationProviderFactory(components =>
						new InternalAuthenticationProviderFactory(components))
				}
			};

			foreach (var potentialPlugin in pluginLoader.Load<IAuthenticationPlugin>()) {
				try {
					var commandLine = potentialPlugin.CommandLineName.ToLowerInvariant();
					Log.Information(
						"Loaded authentication plugin: {plugin} version {version} (Command Line: {commandLine})",
						potentialPlugin.Name, potentialPlugin.Version, commandLine);
					authenticationTypeToPlugin.Add(commandLine,
						new AuthenticationProviderFactory(_ =>
							potentialPlugin.GetAuthenticationProviderFactory(authenticationConfigFile)));
				} catch (CompositionException ex) {
					Log.Error(ex, "Error loading authentication plugin.");
				}
			}

			return authenticationTypeToPlugin.TryGetValue(authenticationType.ToLowerInvariant(), out var factory)
				? factory
				: throw new ApplicationInitializationException(
					$"The authentication type {authenticationType} is not recognised. If this is supposed " +
					$"to be provided by an authentication plugin, confirm the plugin DLL is located in {Locations.PluginsDirectory}." +
					Environment.NewLine +
					$"Valid options for authentication are: {string.Join(", ", authenticationTypeToPlugin.Keys)}.");
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
			if (_dbLock != null && _dbLock.IsAcquired) {
				using (_dbLock) {
					_dbLock.Release();
				}
			}

			return Node.StopAsync(cancellationToken: cancellationToken);
		}
	}
}
