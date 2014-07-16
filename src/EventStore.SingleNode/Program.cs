using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Common.Utils;
using System.Linq;
using EventStore.Web.Users;

namespace EventStore.SingleNode
{
    public class Program : ProgramBase<SingleNodeOptions>
    {
        private SingleVNode _node;
        private Projections.Core.ProjectionsSubsystem _projections;
        private readonly DateTime _startupTimeStamp = DateTime.UtcNow;
        private ExclusiveDbLock _dbLock;

        public static int Main(string[] args)
        {
            var p = new Program();
            return p.Run(args);
        }

        protected override string GetLogsDirectory(SingleNodeOptions options)
        {
            return ResolveDbPath(options.Db, options.HttpPort) + "-logs";
        }

        private string ResolveDbPath(string optionsPath, int nodePort)
        {
            if (optionsPath.IsNotEmptyString())
                return optionsPath;

            return Path.Combine(Path.GetTempPath(),
                                "EventStore",
                                string.Format("{0:yyyy-MM-dd_HH.mm.ss.ffffff}-Node{1}", _startupTimeStamp, nodePort));
        }

        protected override string GetComponentName(SingleNodeOptions options)
        {
            return string.Format("{0}-{1}-single-node", options.Ip, options.HttpPort);
        }

        protected override void Create(SingleNodeOptions opts)
        {
            var dbPath = Path.GetFullPath(ResolveDbPath(opts.Db, opts.HttpPort));

            if (!opts.MemDb)
            {
                _dbLock = new ExclusiveDbLock(dbPath);
                if (!_dbLock.Acquire())
                    throw new Exception(string.Format("Couldn't acquire exclusive lock on DB at '{0}'.", dbPath));
            }

            FileStreamExtensions.ConfigureFlush(disableFlushToDisk: opts.UnsafeDisableFlushToDisk);
            var db = new TFChunkDb(CreateDbConfig(dbPath, opts.CachedChunks, opts.ChunksCacheSize, opts.MemDb));
            var vnodeSettings = GetVNodeSettings(opts);
            var dbVerifyHashes = !opts.SkipDbVerify;
            var runProjections = opts.RunProjections;

            Log.Info("\n{0,-25} {1}\n{2,-25} {3} (0x{3:X})\n{4,-25} {5} (0x{5:X})\n{6,-25} {7} (0x{7:X})\n{8,-25} {9} (0x{9:X})\n",
                     "DATABASE:", db.Config.Path,
                     "WRITER CHECKPOINT:", db.Config.WriterCheckpoint.Read(),
                     "CHASER CHECKPOINT:", db.Config.ChaserCheckpoint.Read(),
                     "EPOCH CHECKPOINT:", db.Config.EpochCheckpoint.Read(),
                     "TRUNCATE CHECKPOINT:", db.Config.TruncateCheckpoint.Read());

            var enabledNodeSubsystems = runProjections >= ProjectionType.System
                ? new[] {NodeSubsystems.Projections}
                : new NodeSubsystems[0];
            _projections = new Projections.Core.ProjectionsSubsystem(opts.ProjectionThreads, runProjections);
            _node = new SingleVNode(db, vnodeSettings, dbVerifyHashes, opts.MaxMemTableSize, _projections);
            RegisterWebControllers(enabledNodeSubsystems);
            RegisterUIProjections();
        }

        private void RegisterUIProjections()
        {
            var users = new UserManagementProjectionsRegistration();
            _node.MainBus.Subscribe(users);
        }

        private void RegisterWebControllers(NodeSubsystems[] enabledNodeSubsystems)
        {
            _node.HttpService.SetupController(new WebSiteController(_node.MainQueue, enabledNodeSubsystems));
            _node.HttpService.SetupController(new UsersWebController(_node.MainQueue));
        }

        private static SingleVNodeSettings GetVNodeSettings(SingleNodeOptions options)
        {
            X509Certificate2 certificate = null;
            if (options.SecureTcpPort > 0)
            {
                if (options.CertificateStoreName.IsNotEmptyString())
                    certificate = LoadCertificateFromStore(options.CertificateStoreLocation, options.CertificateStoreName, options.CertificateSubjectName, options.CertificateThumbprint);
                else if (options.CertificateFile.IsNotEmptyString())
                    certificate = LoadCertificateFromFile(options.CertificateFile, options.CertificatePassword);
                else
                    throw new Exception("No server certificate specified.");
            }

            var tcpEndPoint = new IPEndPoint(options.Ip, options.TcpPort);
            var secureTcpEndPoint = options.SecureTcpPort > 0 ? new IPEndPoint(options.Ip, options.SecureTcpPort) : null;
            var httpEndPoint = new IPEndPoint(options.Ip, options.HttpPort);
            var prefixes = options.HttpPrefixes.IsNotEmpty() ? options.HttpPrefixes : new[] {httpEndPoint.ToHttpUrl()};
            var vnodeSettings = new SingleVNodeSettings(tcpEndPoint,
                                                        secureTcpEndPoint,
                                                        httpEndPoint, 
                                                        prefixes.Select(p => p.Trim()).ToArray(),
                                                        options.EnableTrustedAuth,
                                                        certificate,
                                                        options.WorkerThreads, TimeSpan.FromMilliseconds(options.MinFlushDelayMs),
                                                        TimeSpan.FromMilliseconds(options.PrepareTimeoutMs),
                                                        TimeSpan.FromMilliseconds(options.CommitTimeoutMs),
                                                        TimeSpan.FromSeconds(options.StatsPeriodSec),
                                                        TimeSpan.FromMilliseconds(options.TcpTimeout),
                                                        StatsStorage.StreamAndCsv,
                                                        false,
                                                        options.DisableScavengeMerging);
            return vnodeSettings;
        }
        protected override void Start()
        {
            _node.Start();
        }

        public override void Stop()
        {
            _node.Stop(exitProcess: true);
        }

        protected override void OnProgramExit()
        {
            base.OnProgramExit();

            if (_dbLock != null && _dbLock.IsAcquired)
                _dbLock.Release();
        }
    }
}
