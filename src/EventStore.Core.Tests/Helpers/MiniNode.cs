using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Settings;
using EventStore.Core.Tests.Http;
using EventStore.Core.Tests.Services.Transport.Tcp;
using EventStore.Core.Services.Gossip;
using EventStore.Core.Cluster.Settings;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.Tests.Helpers
{
    public class MiniNode
    {
        private static bool _running;

        public static int RunCount = 0;
        public static readonly Stopwatch RunningTime = new Stopwatch();
        public static readonly Stopwatch StartingTime = new Stopwatch();
        public static readonly Stopwatch StoppingTime = new Stopwatch();

        public const int ChunkSize = 1024*1024;
        public const int CachedChunkSize = ChunkSize + ChunkHeader.Size + ChunkFooter.Size;

        private static readonly ILogger Log = LogManager.GetLoggerFor<MiniNode>();

        public IPEndPoint TcpEndPoint { get; private set; }
        public IPEndPoint TcpSecEndPoint { get; private set; }
        public IPEndPoint HttpEndPoint { get; private set; }

        public readonly ClusterVNode Node;
        public readonly TFChunkDb Db;
        private readonly string _dbPath;

        public MiniNode(string pathname, 
                        int? tcpPort = null, int? tcpSecPort = null, int? httpPort = null, 
                        ISubsystem[] subsystems = null,
                        int? chunkSize = null, int? cachedChunkSize = null, bool enableTrustedAuth = false, bool skipInitializeStandardUsersCheck = true,
                        int memTableSize = 1000,
                        bool inMemDb = true, bool disableFlushToDisk = false)
        {
            if (_running) throw new Exception("Previous MiniNode is still running!!!");
            _running = true;

            RunningTime.Start();
            RunCount += 1;

            IPAddress ip = IPAddress.Loopback; //GetLocalIp();

            int extTcpPort = tcpPort ?? PortsHelper.GetAvailablePort(ip);
            int extSecTcpPort = tcpSecPort ?? PortsHelper.GetAvailablePort(ip);
            int extHttpPort = httpPort ?? PortsHelper.GetAvailablePort(ip);

            _dbPath = Path.Combine(pathname, string.Format("mini-node-db-{0}-{1}-{2}", extTcpPort, extSecTcpPort, extHttpPort));
            Directory.CreateDirectory(_dbPath);
            FileStreamExtensions.ConfigureFlush(disableFlushToDisk);
            Db = new TFChunkDb(CreateDbConfig(chunkSize ?? ChunkSize, _dbPath, cachedChunkSize ?? CachedChunkSize, inMemDb));
            
            TcpEndPoint = new IPEndPoint(ip, extTcpPort);
            TcpSecEndPoint = new IPEndPoint(ip, extSecTcpPort);
            HttpEndPoint = new IPEndPoint(ip, extHttpPort);
            var vNodeSettings = new ClusterVNodeSettings(Guid.NewGuid(),
                                                         0,
                                                         null,
                                                         null,
                                                         TcpEndPoint,
                                                         TcpSecEndPoint,
                                                         null,
                                                         HttpEndPoint,
                                                         new [] {HttpEndPoint.ToHttpUrl()},
                                                         enableTrustedAuth,
                                                         ssl_connections.GetCertificate(),
                                                         1,
                                                         false,
                                                         "whatever",
                                                         new IPEndPoint[] {},
                                                         TFConsts.MinFlushDelayMs,
                                                         1,
                                                         1,
                                                         1,
                                                         TimeSpan.FromSeconds(2),
                                                         TimeSpan.FromSeconds(2),
                                                         false,
                                                         "",
                                                         false,
                                                         TimeSpan.FromHours(1),
                                                         StatsStorage.None,
                                                         1,
                                                         null,
                                                         true,
                                                         true,
                                                         true,
                                                         false,
                                                         TimeSpan.FromSeconds(30),
                                                         TimeSpan.FromSeconds(30),
                                                         TimeSpan.FromSeconds(10),
                                                         TimeSpan.FromSeconds(10));
 /*           var singleVNodeSettings = new SingleVNodeSettings(TcpEndPoint,
                                                              TcpSecEndPoint,
                                                              HttpEndPoint,
                                                              new[] { HttpEndPoint.ToHttpUrl() },
                                                              enableTrustedAuth,
                                                              ssl_connections.GetCertificate(),
                                                              1,
                                                              TFConsts.MinFlushDelayMs,
                                                              1,
                                                              1,
                                                              1,
                                                              TimeSpan.FromSeconds(2),
                                                              TimeSpan.FromSeconds(2),
                                                              TimeSpan.FromHours(1),
                                                              TimeSpan.FromSeconds(10),
                                                              StatsStorage.None,
                                                              skipInitializeStandardUsersCheck: skipInitializeStandardUsersCheck );
*/
            Log.Info("\n{0,-25} {1} ({2}/{3}, {4})\n"
                     + "{5,-25} {6} ({7})\n"
                     + "{8,-25} {9} ({10}-bit)\n"
                     + "{11,-25} {12}\n"
                     + "{13,-25} {14}\n"
                     + "{15,-25} {16}\n"
                     + "{17,-25} {18}\n"
                     + "{19,-25} {20}\n\n",
                     "ES VERSION:", VersionInfo.Version, VersionInfo.Branch, VersionInfo.Hashtag, VersionInfo.Timestamp,
                     "OS:", OS.OsFlavor, Environment.OSVersion,
                     "RUNTIME:", OS.GetRuntimeVersion(), Marshal.SizeOf(typeof(IntPtr)) * 8,
                     "GC:", GC.MaxGeneration == 0 ? "NON-GENERATION (PROBABLY BOEHM)" : string.Format("{0} GENERATIONS", GC.MaxGeneration + 1),
                     "DBPATH:", _dbPath,
                     "TCP ENDPOINT:", TcpEndPoint,
                     "TCP SECURE ENDPOINT:", TcpSecEndPoint,
                     "HTTP ENDPOINT:", HttpEndPoint);
            Node = new ClusterVNode(Db,
                                    vNodeSettings,         
                                   new KnownEndpointGossipSeedSource(new [] {HttpEndPoint}),
                                   false,
                                   memTableSize,
                                   subsystems : subsystems);

            Node.ExternalHttpService.SetupController(new TestController(Node.MainQueue));
        }

        public void Start()
        {
            StartingTime.Start();

            var startedEvent = new ManualResetEventSlim(false);
            Node.MainBus.Subscribe(
                new AdHocHandler<UserManagementMessage.UserManagementServiceInitialized>(m => startedEvent.Set()));

            Node.Start();

            if (!startedEvent.Wait(60000))
                throw new TimeoutException("MiniNode haven't started in 60 seconds.");

            StartingTime.Stop();
        }

        public void Shutdown(bool keepDb = false, bool keepPorts = false)
        {
            StoppingTime.Start();

            var shutdownEvent = new ManualResetEventSlim(false);
            Node.MainBus.Subscribe(new AdHocHandler<SystemMessage.BecomeShutdown>(m => shutdownEvent.Set()));

            Node.Stop();

            if (!shutdownEvent.Wait(20000))
                throw new TimeoutException("MiniNode haven't shut down in 20 seconds.");
            
            if (!keepPorts)
            {
                PortsHelper.ReturnPort(TcpEndPoint.Port);
                PortsHelper.ReturnPort(TcpSecEndPoint.Port);
                PortsHelper.ReturnPort(HttpEndPoint.Port);
            }
            
            if (!keepDb)
                TryDeleteDirectory(_dbPath);

            StoppingTime.Stop();
            RunningTime.Stop();

            _running = false;
        }

        private void TryDeleteDirectory(string directory)
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to remove directory {0}", directory);
                Debug.WriteLine(e);
            }
        }

        private IPAddress GetLocalIp()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        }

        private TFChunkDbConfig CreateDbConfig(int chunkSize, string dbPath, long chunksCacheSize, bool inMemDb)
        {
            ICheckpoint writerChk;
            ICheckpoint chaserChk;
            ICheckpoint epochChk;
            ICheckpoint truncateChk;
            if (inMemDb)
            {
                writerChk = new InMemoryCheckpoint(Checkpoint.Writer);
                chaserChk = new InMemoryCheckpoint(Checkpoint.Chaser);
                epochChk = new InMemoryCheckpoint(Checkpoint.Epoch, initValue: -1);
                truncateChk = new InMemoryCheckpoint(Checkpoint.Truncate, initValue: -1);
            }
            else
            {
                var writerCheckFilename = Path.Combine(dbPath, Checkpoint.Writer + ".chk");
                var chaserCheckFilename = Path.Combine(dbPath, Checkpoint.Chaser + ".chk");
                var epochCheckFilename = Path.Combine(dbPath, Checkpoint.Epoch + ".chk");
                var truncateCheckFilename = Path.Combine(dbPath, Checkpoint.Truncate + ".chk");
                if (Runtime.IsMono)
                {
                    writerChk = new FileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
                    chaserChk = new FileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);
                    epochChk = new FileCheckpoint(epochCheckFilename, Checkpoint.Epoch, cached: true, initValue: -1);
                    truncateChk = new FileCheckpoint(truncateCheckFilename, Checkpoint.Truncate, cached: true, initValue: -1);
                }
                else
                {
                    writerChk = new MemoryMappedFileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
                    chaserChk = new MemoryMappedFileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);
                    epochChk = new MemoryMappedFileCheckpoint(epochCheckFilename, Checkpoint.Epoch, cached: true, initValue: -1);
                    truncateChk = new MemoryMappedFileCheckpoint(truncateCheckFilename, Checkpoint.Truncate, cached: true, initValue: -1);
                }
            }
            var nodeConfig = new TFChunkDbConfig(dbPath,
                                                 new VersionedPatternFileNamingStrategy(dbPath, "chunk-"),
                                                 chunkSize,
                                                 chunksCacheSize,
                                                 writerChk,
                                                 chaserChk,
                                                 epochChk,
                                                 truncateChk,
                                                 inMemDb);
            return nodeConfig;
        }
    }
}
