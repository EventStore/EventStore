﻿using System;
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
using EventStore.Core.Cluster.Settings;
using EventStore.Core.PluginModel;
using EventStore.Core.Services.Gossip;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;
using System.Net.NetworkInformation;
using EventStore.Core.Data;

namespace EventStore.ClusterNode
{
    public class Program : ProgramBase<ClusterNodeOptions>
    {
        private ClusterVNode _node;
        private Projections.Core.ProjectionsSubsystem _projections;
        private ExclusiveDbLock _dbLock;
        private ClusterNodeMutex _clusterNodeMutex;

        public static int Main(string[] args)
        {
            var p = new Program();
            return p.Run(args);
        }

        protected override string GetLogsDirectory(ClusterNodeOptions options)
        {
            return options.Log;
        }

        protected override string GetComponentName(ClusterNodeOptions options)
        {
            return string.Format("{0}-{1}-cluster-node", options.ExtIp, options.ExtHttpPort);
        }

        protected override void PreInit(ClusterNodeOptions options)
        {
            base.PreInit(options);

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
                Log.Error("Cannot override the minimum number of Threadpool threads (machine default: {0}, specified value: {1})", minWorkerThreads, options.MonoMinThreadpoolSize);
        }

        protected override void Create(ClusterNodeOptions opts)
        {
            var dbPath = opts.Db;

            if (!opts.MemDb)
            {
                _dbLock = new ExclusiveDbLock(dbPath);
                if (!_dbLock.Acquire())
                    throw new Exception(string.Format("Couldn't acquire exclusive lock on DB at '{0}'.", dbPath));
            }
            _clusterNodeMutex = new ClusterNodeMutex();
            if (!_clusterNodeMutex.Acquire())
                throw new Exception(string.Format("Couldn't acquire exclusive Cluster Node mutex '{0}'.", _clusterNodeMutex.MutexName));

            var dbConfig = CreateDbConfig(dbPath, opts.CachedChunks, opts.ChunksCacheSize, opts.MemDb);
            FileStreamExtensions.ConfigureFlush(disableFlushToDisk: opts.UnsafeDisableFlushToDisk);
            var db = new TFChunkDb(dbConfig);
            var vNodeSettings = GetClusterVNodeSettings(opts);

            IGossipSeedSource gossipSeedSource;
            if (opts.DiscoverViaDns)
            {
                gossipSeedSource = new DnsGossipSeedSource(opts.ClusterDns, opts.ClusterGossipPort);
            }
            else
            {
                if (opts.GossipSeed.Length == 0)
                {
                    if (opts.ClusterSize > 1)
                    {
                        Log.Error("DNS discovery is disabled, but no gossip seed endpoints have been specified. "
                                + "Specify gossip seeds using the --gossip-seed command line option.");
                    }
                    else
                    {
                        Log.Info("DNS discovery is disabled, but no gossip seed endpoints have been specified. Since"
                                + "the cluster size is set to 1, this may be intentional. Gossip seeds can be specified"
                                + "seeds using the --gossip-seed command line option.");
                    }
                }

                gossipSeedSource = new KnownEndpointGossipSeedSource(opts.GossipSeed);
            }

            var runProjections = opts.RunProjections;

            Log.Info("\n{0,-25} {1}\n"
                    + "{2,-25} {3}\n"
                    + "{4,-25} {5} (0x{5:X})\n"
                    + "{6,-25} {7} (0x{7:X})\n"
                    + "{8,-25} {9} (0x{9:X})\n"
                    + "{10,-25} {11} (0x{11:X})\n",
                    "INSTANCE ID:", vNodeSettings.NodeInfo.InstanceId,
                    "DATABASE:", db.Config.Path,
                    "WRITER CHECKPOINT:", db.Config.WriterCheckpoint.Read(),
                    "CHASER CHECKPOINT:", db.Config.ChaserCheckpoint.Read(),
                    "EPOCH CHECKPOINT:", db.Config.EpochCheckpoint.Read(),
                    "TRUNCATE CHECKPOINT:", db.Config.TruncateCheckpoint.Read());

            var enabledNodeSubsystems = runProjections >= ProjectionType.System
                ? new[] { NodeSubsystems.Projections }
            : new NodeSubsystems[0];
            _projections = new Projections.Core.ProjectionsSubsystem(opts.ProjectionThreads, opts.RunProjections, opts.DevelopmentMode);
            var infoController = new InfoController(opts, opts.RunProjections);
            _node = new ClusterVNode(db, vNodeSettings, gossipSeedSource, infoController, _projections);
            RegisterWebControllers(enabledNodeSubsystems, vNodeSettings);
        }

        private void RegisterWebControllers(NodeSubsystems[] enabledNodeSubsystems, ClusterVNodeSettings settings)
        {
            if (_node.InternalHttpService != null)
            {
                _node.InternalHttpService.SetupController(new ClusterWebUiController(_node.MainQueue, enabledNodeSubsystems));
            }
            if (settings.AdminOnPublic)
            {
                _node.ExternalHttpService.SetupController(new ClusterWebUiController(_node.MainQueue, enabledNodeSubsystems));
            }
        }

        private static int GetQuorumSize(int clusterSize)
        {
            if (clusterSize == 1) return 1;
            return clusterSize / 2 + 1;
        }

        private static ClusterVNodeSettings GetClusterVNodeSettings(ClusterNodeOptions options)
        {
            X509Certificate2 certificate = null;
            if (options.IntSecureTcpPort > 0 || options.ExtSecureTcpPort > 0)
            {
                if (options.CertificateStoreName.IsNotEmptyString())
                    certificate = LoadCertificateFromStore(options.CertificateStoreLocation, options.CertificateStoreName, options.CertificateSubjectName, options.CertificateThumbprint);
                else if (options.CertificateFile.IsNotEmptyString())
                    certificate = LoadCertificateFromFile(options.CertificateFile, options.CertificatePassword);
                else
                    throw new Exception("No server certificate specified.");
            }

            var intHttp = new IPEndPoint(options.IntIp, options.IntHttpPort);
            var extHttp = new IPEndPoint(options.ExtIp, options.ExtHttpPort);
            var intTcp = new IPEndPoint(options.IntIp, options.IntTcpPort);
            var intSecTcp = options.IntSecureTcpPort > 0 ? new IPEndPoint(options.IntIp, options.IntSecureTcpPort) : null;
            var extTcp = new IPEndPoint(options.ExtIp, options.ExtTcpPort);
            var extSecTcp = options.ExtSecureTcpPort > 0 ? new IPEndPoint(options.ExtIp, options.ExtSecureTcpPort) : null;
            var intHttpPrefixes = options.IntHttpPrefixes.IsNotEmpty() ? options.IntHttpPrefixes : new string[0];
            var extHttpPrefixes = options.ExtHttpPrefixes.IsNotEmpty() ? options.ExtHttpPrefixes : new string[0];
            var quorumSize = GetQuorumSize(options.ClusterSize);


            GossipAdvertiseInfo gossipAdvertiseInfo = null;

            var additionalIntHttpPrefixes = new List<string>(intHttpPrefixes);
            var additionalExtHttpPrefixes = new List<string>(extHttpPrefixes);
            if ((options.IntIp.Equals(IPAddress.Parse("0.0.0.0")) ||
                options.ExtIp.Equals(IPAddress.Parse("0.0.0.0"))) && options.AddInterfacePrefixes)
            {
                IPAddress nonLoopbackAddress = null; 
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            if (nonLoopbackAddress == null && !IPAddress.IsLoopback(address.Address))
                            {
                                nonLoopbackAddress = address.Address;
                            }
                            additionalIntHttpPrefixes.Add(String.Format("http://{0}:{1}/", address.Address, intHttp.Port));
                            additionalExtHttpPrefixes.Add(String.Format("http://{0}:{1}/", address.Address, extHttp.Port));
                        }
                    }
                }
                if (gossipAdvertiseInfo == null)
                {
                    IPAddress addressToAdvertise = options.ClusterSize > 1 ? nonLoopbackAddress : IPAddress.Loopback;
                    gossipAdvertiseInfo = new GossipAdvertiseInfo(new IPEndPoint(options.IntIpAdvertiseAs ?? addressToAdvertise,
                                                                      options.IntTcpPortAdvertiseAs > 0 ? 
                                                                      options.IntTcpPortAdvertiseAs : options.IntTcpPort),
                                                                  options.IntSecureTcpPort > 0 ? new IPEndPoint(addressToAdvertise,
                                                                      options.IntSecureTcpPort) : null,
                                                                  new IPEndPoint(options.ExtIpAdvertiseAs ?? addressToAdvertise,
                                                                      options.ExtTcpPortAdvertiseAs > 0 ? 
                                                                      options.ExtTcpPortAdvertiseAs : options.ExtTcpPort),
                                                                  options.ExtSecureTcpPort > 0 ? new IPEndPoint(addressToAdvertise,
                                                                      options.ExtSecureTcpPort) : null,
                                                                  new IPEndPoint(options.IntIpAdvertiseAs ?? addressToAdvertise,
                                                                      options.IntHttpPortAdvertiseAs > 0 ?
                                                                      options.IntHttpPortAdvertiseAs : options.IntHttpPort),
                                                                  new IPEndPoint(options.ExtIpAdvertiseAs ?? addressToAdvertise,
                                                                      options.ExtHttpPortAdvertiseAs > 0 ?
                                                                      options.ExtHttpPortAdvertiseAs : options.ExtHttpPort));
                }
            }
            else if (options.AddInterfacePrefixes)
            {
                additionalIntHttpPrefixes.Add(String.Format("http://{0}:{1}/", options.IntIp, options.IntHttpPort));
                additionalExtHttpPrefixes.Add(String.Format("http://{0}:{1}/", options.ExtIp, options.ExtHttpPort));
            }
            if (!intHttpPrefixes.Contains(x => x.Contains("localhost")) && options.AddInterfacePrefixes)
            {
                if (options.IntIp.Equals(IPAddress.Parse("0.0.0.0")) ||
                   Equals(intHttp.Address, IPAddress.Loopback))
                {
                    additionalIntHttpPrefixes.Add(string.Format("http://localhost:{0}/", intHttp.Port));
                }
            }
            if (!extHttpPrefixes.Contains(x => x.Contains("localhost")) && options.AddInterfacePrefixes)
            {
                if (options.ExtIp.Equals(IPAddress.Parse("0.0.0.0")) ||
                   Equals(extHttp.Address, IPAddress.Loopback))
                {
                    additionalExtHttpPrefixes.Add(string.Format("http://localhost:{0}/", extHttp.Port));
                }
            }
            intHttpPrefixes = additionalIntHttpPrefixes.ToArray();
            extHttpPrefixes = additionalExtHttpPrefixes.ToArray();

            if (gossipAdvertiseInfo == null)
            {
                gossipAdvertiseInfo = new GossipAdvertiseInfo(new IPEndPoint(options.IntIpAdvertiseAs ?? options.IntIp, 
                                                                options.IntTcpPortAdvertiseAs > 0 ?
                                                                options.IntTcpPortAdvertiseAs : options.IntTcpPort),
                                                              intSecTcp,
                                                              new IPEndPoint(options.ExtIpAdvertiseAs ?? options.ExtIp,
                                                                  options.ExtTcpPortAdvertiseAs > 0 ?
                                                                  options.ExtTcpPortAdvertiseAs : options.ExtTcpPort),
                                                              extSecTcp,
                                                              new IPEndPoint(options.IntIpAdvertiseAs ?? options.IntIp,
                                                                  options.IntHttpPortAdvertiseAs >0 ?
                                                                  options.IntHttpPortAdvertiseAs : options.IntHttpPort),
                                                              new IPEndPoint(options.ExtIpAdvertiseAs ?? options.ExtIp,
                                                                  options.ExtHttpPortAdvertiseAs > 0 ?
                                                                  options.ExtHttpPortAdvertiseAs : options.ExtHttpPort));
            }

            var prepareCount = options.PrepareCount > quorumSize ? options.PrepareCount : quorumSize;
            var commitCount = options.CommitCount > quorumSize ? options.CommitCount : quorumSize;
            Log.Info("Quorum size set to " + prepareCount);
            if (options.UseInternalSsl)
            {
                if (ReferenceEquals(options.SslTargetHost, Opts.SslTargetHostDefault)) throw new Exception("No SSL target host specified.");
                if (intSecTcp == null) throw new Exception("Usage of internal secure communication is specified, but no internal secure endpoint is specified!");
            }

            var authenticationProviderFactory = GetAuthenticationProviderFactory(options.AuthenticationType, options.Config);

            return new ClusterVNodeSettings(Guid.NewGuid(), 0,
                    intTcp, intSecTcp, extTcp, extSecTcp, intHttp, extHttp, gossipAdvertiseInfo,
                    intHttpPrefixes, extHttpPrefixes, options.EnableTrustedAuth,
                    certificate,
                    options.WorkerThreads, options.DiscoverViaDns,
                    options.ClusterDns, options.GossipSeed,
                    TimeSpan.FromMilliseconds(options.MinFlushDelayMs), options.ClusterSize,
                    prepareCount, commitCount,
                    TimeSpan.FromMilliseconds(options.PrepareTimeoutMs),
                    TimeSpan.FromMilliseconds(options.CommitTimeoutMs),
                    options.UseInternalSsl, options.SslTargetHost, options.SslValidateServer,
                    TimeSpan.FromSeconds(options.StatsPeriodSec), StatsStorage.StreamAndCsv,
                    options.NodePriority, authenticationProviderFactory, options.DisableScavengeMerging,
                    options.AdminOnExt, options.StatsOnExt, options.GossipOnExt,
                    TimeSpan.FromMilliseconds(options.GossipIntervalMs),
                    TimeSpan.FromMilliseconds(options.GossipAllowedDifferenceMs),
                    TimeSpan.FromMilliseconds(options.GossipTimeoutMs),
                    TimeSpan.FromMilliseconds(options.ExtTcpHeartbeatTimeout),
                    TimeSpan.FromMilliseconds(options.ExtTcpHeartbeatInterval),
                    TimeSpan.FromMilliseconds(options.IntTcpHeartbeatTimeout),
                    TimeSpan.FromMilliseconds(options.IntTcpHeartbeatInterval),
                    !options.SkipDbVerify, options.MaxMemTableSize,
                    options.DevelopmentMode,
                    options.EnableHistograms,
                    options.IndexCacheDepth);
        }

        private static IAuthenticationProviderFactory GetAuthenticationProviderFactory(string authenticationType, string authenticationConfigFile)
        {
            var catalog = new AggregateCatalog();

            var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var pluginsPath = Path.Combine(currentPath ?? String.Empty, "plugins");

            catalog.Catalogs.Add(new AssemblyCatalog(typeof(Program).Assembly));

            if (Directory.Exists(pluginsPath))
            {
                Log.Info("Plugins path: {0}", pluginsPath);
                catalog.Catalogs.Add(new DirectoryCatalog(pluginsPath));
            }
            else
            {
                Log.Info("Cannot find plugins path: {0}", pluginsPath);
            }

            var compositionContainer = new CompositionContainer(catalog);
            var potentialPlugins = compositionContainer.GetExports<IAuthenticationPlugin>();

            var authenticationTypeToPlugin = new Dictionary<string, Func<IAuthenticationProviderFactory>> {
                { "internal", () => new InternalAuthenticationProviderFactory() }
            };

            foreach (var potentialPlugin in potentialPlugins)
            {
                try
                {
                    var plugin = potentialPlugin.Value;
                    var commandLine = plugin.CommandLineName.ToLowerInvariant();
                    Log.Info("Loaded authentication plugin: {0} version {1} (Command Line: {2})", plugin.Name, plugin.Version, commandLine);
                    authenticationTypeToPlugin.Add(commandLine, () => plugin.GetAuthenticationProviderFactory(authenticationConfigFile));
                }
                catch (CompositionException ex)
                {
                    Log.ErrorException(ex, "Error loading authentication plugin.");
                }
            }

            Func<IAuthenticationProviderFactory> factory;
            if (!authenticationTypeToPlugin.TryGetValue(authenticationType.ToLowerInvariant(), out factory))
            {
                throw new ApplicationInitializationException(string.Format("The authentication type {0} is not recognised. If this is supposed " +
                            "to be provided by an authentication plugin, confirm the plugin DLL is located in {1}.\n" +
                            "Valid options for authentication are: {2}.", authenticationType, pluginsPath, string.Join(", ", authenticationTypeToPlugin.Keys)));
            }

            return factory();
        }

        protected override void Start()
        {
            _node.Start();
        }

        public override void Stop()
        {
            _node.StopNonblocking(true, true);
        }

        protected override void OnProgramExit()
        {
            base.OnProgramExit();

            if (_dbLock != null && _dbLock.IsAcquired)
                _dbLock.Release();
        }
    }
}
