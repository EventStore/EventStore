using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core;
using EventStore.Core.Cluster.Settings;
using EventStore.Core.Services.Gossip;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;
using EventStore.Web.Users;

namespace EventStore.ClusterNode
{
    public class Program : ProgramBase<ClusterNodeOptions>
    {
        private ClusterVNode _node;
        private Projections.Core.ProjectionsSubsystem _projections;
        private readonly DateTime _startupTimeStamp = DateTime.UtcNow;
        private ExclusiveDbLock _dbLock;
        private ClusterNodeMutex _clusterNodeMutex;

        public static int Main(string[] args)
        {
            var p = new Program();
            return p.Run(args);
        }

        protected override string GetLogsDirectory(ClusterNodeOptions options)
        {
            return ResolveDbPath(options.DbPath, options.ExternalHttpPort) + "-logs";
        }

        protected override string GetComponentName(ClusterNodeOptions options)
        {
            return string.Format("{0}-{1}-cluster-node", options.ExternalIp, options.ExternalHttpPort);
        }

        private string ResolveDbPath(string optionsPath, int nodePort)
        {
            if (optionsPath.IsNotEmptyString())
                return optionsPath;

            return Path.Combine(Path.GetTempPath(),
                                "EventStore",
                                string.Format("{0:yyyy-MM-dd_HH.mm.ss.ffffff}-Node{1}", _startupTimeStamp, nodePort));
        }

        protected override void Create(ClusterNodeOptions opts)
        {
            var dbPath = Path.GetFullPath(ResolveDbPath(opts.DbPath, opts.ExternalHttpPort));

            if (!opts.InMemDb)
            {
                _dbLock = new ExclusiveDbLock(dbPath);
                if (!_dbLock.Acquire())
                    throw new Exception(string.Format("Couldn't acquire exclusive lock on DB at '{0}'.", dbPath));
            }
            _clusterNodeMutex = new ClusterNodeMutex();
            if (!_clusterNodeMutex.Acquire())
                throw new Exception(string.Format("Couldn't acquire exclusive Cluster Node mutex '{0}'.", _clusterNodeMutex.MutexName));

            var dbConfig = CreateDbConfig(dbPath, opts.CachedChunks, opts.ChunksCacheSize, opts.InMemDb);
            var db = new TFChunkDb(dbConfig);
            var vNodeSettings = GetClusterVNodeSettings(opts);
            var managersIps = opts.FakeDnsIps.Union(new[] { vNodeSettings.ManagerEndPoint.Address }).Distinct().ToArray();
            var dnsService = opts.FakeDns ? (IDnsService)new ConfigDns(managersIps) : new DnsService();
            var dbVerifyHashes = !opts.SkipDbVerify;
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

            var enabledNodeSubsystems = runProjections >= RunProjections.System
                ? new[] {NodeSubsystems.Projections}
                : new NodeSubsystems[0];
            _projections = new Projections.Core.ProjectionsSubsystem(opts.ProjectionThreads, opts.RunProjections);
            _node = new ClusterVNode(db, vNodeSettings, dnsService, dbVerifyHashes, ESConsts.MemTableEntryCount, _projections);
            RegisterWebControllers(enabledNodeSubsystems);
            RegisterUiProjections();
        }

        private void RegisterUiProjections()
        {
            var users = new UserManagementProjectionsRegistration();
            _node.MainBus.Subscribe(users);
        }

        private void RegisterWebControllers(NodeSubsystems[] enabledNodeSubsystems)
        {
            _node.InternalHttpService.SetupController(new ClusterWebUIController(_node.MainQueue, enabledNodeSubsystems));
            _node.ExternalHttpService.SetupController(new ClusterWebUIController(_node.MainQueue, enabledNodeSubsystems));
            _node.InternalHttpService.SetupController(new UsersWebController(_node.MainQueue));
            _node.ExternalHttpService.SetupController(new UsersWebController(_node.MainQueue));
        }

        private static ClusterVNodeSettings GetClusterVNodeSettings(ClusterNodeOptions options)
        {
            X509Certificate2 certificate = null;
            if (options.InternalSecureTcpPort > 0 || options.ExternalSecureTcpPort > 0)
            {
                if (options.CertificateStore.IsNotEmptyString())
                    certificate = LoadCertificateFromStore(options.CertificateStore, options.CertificateName);
                else if (options.CertificateFile.IsNotEmptyString())
                    certificate = LoadCertificateFromFile(options.CertificateFile, options.CertificatePassword);
                else
                    throw new Exception("No server certificate specified.");
            }

            var intHttp = new IPEndPoint(options.InternalIp, options.InternalHttpPort);
            var extHttp = new IPEndPoint(options.ExternalIp, options.ExternalHttpPort);
            var intTcp = new IPEndPoint(options.InternalIp, options.InternalTcpPort);
            var intSecTcp = options.InternalSecureTcpPort > 0 ? new IPEndPoint(options.InternalIp, options.InternalSecureTcpPort) : null;
            var extTcp = new IPEndPoint(options.ExternalIp, options.ExternalTcpPort);
            var extSecTcp = options.ExternalSecureTcpPort > 0 ? new IPEndPoint(options.ExternalIp, options.ExternalSecureTcpPort) : null;
            var manager = new IPEndPoint(options.InternalManagerIp, options.InternalManagerHttpPort);
            var prefixes = options.HttpPrefixes.IsNotEmpty() ? options.HttpPrefixes : new[] { extHttp.ToHttpUrl() };

            if (options.UseInternalSsl)
            {
                if (ReferenceEquals(options.SslTargetHost, Opts.SslTargetHostDefault)) throw new Exception("No SSL target host specified.");
                if (intSecTcp == null) throw new Exception("Usage of internal secure communication is specified, but no internal secure endpoint is specified!");
            }

			//Default to internal authentication
			const ClusterVNodeAuthenticationType authenticationType = ClusterVNodeAuthenticationType.Internal;
	        
	        return new ClusterVNodeSettings(Guid.NewGuid(),
	                                        intTcp, intSecTcp, extTcp, extSecTcp, intHttp, extHttp,
	                                        prefixes, options.EnableTrustedAuth,
	                                        certificate,
	                                        options.WorkerThreads,
	                                        manager,
	                                        options.ClusterDns, options.ClusterSize, options.FakeDns,
	                                        options.MinFlushDelayMs,
	                                        options.PrepareCount, options.CommitCount,
	                                        TimeSpan.FromMilliseconds(options.PrepareTimeoutMs),
	                                        TimeSpan.FromMilliseconds(options.CommitTimeoutMs),
	                                        options.UseInternalSsl, options.SslTargetHost, options.SslValidateServer,
	                                        TimeSpan.FromSeconds(options.StatsPeriodSec), StatsStorage.StreamAndCsv,
	                                        authenticationType);
        }

        protected override void Start()
        {
            _node.Start();
        }

        public override void Stop()
        {
            _node.Stop();
        }

        protected override void OnProgramExit()
        {
            base.OnProgramExit();

            if (_dbLock != null && _dbLock.IsAcquired)
                _dbLock.Release();
        }
    }
}