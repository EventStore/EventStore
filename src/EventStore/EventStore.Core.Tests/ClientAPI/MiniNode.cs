using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EventStore.Common.Settings;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Tests.ClientAPI
{
    internal class MiniNode
    {
        private static volatile MiniNode _instance;
        private static readonly object InstanceLock = new object();

        private readonly SingleVNode _node;
        private readonly TFChunkDb _tfChunkDb;
        private ICheckpoint _writerChk;
        private ICheckpoint _chaserChk;

        private readonly string _oneTimeDbPath;

        public IPEndPoint TcpEndPoint { get; private set; }
        public IPEndPoint HttpEndPoint { get; private set; }

        public static MiniNode Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new MiniNode();
                    }
                }
                return _instance;
            }
        }

        public static MiniNode Create(int externalTcpPort, int externalHttpPort)
        {
            return new MiniNode(externalTcpPort, externalHttpPort);
        }

        private MiniNode(int externalTcpPort = 4222, int externalHttpPort = 5222)
        {
            _oneTimeDbPath = Path.Combine(Path.GetTempPath(), string.Format("mini-node-one-time-db-{0}-{1}", externalTcpPort, externalHttpPort));
            TryDeleteDirectory(_oneTimeDbPath);
            Directory.CreateDirectory(_oneTimeDbPath);
            _tfChunkDb = new TFChunkDb(CreateOneTimeDbConfig(256*1024*1024, _oneTimeDbPath, 2));

            var ip = GetLocalIp();
            TcpEndPoint = new IPEndPoint(ip, externalTcpPort);
            HttpEndPoint = new IPEndPoint(ip, externalHttpPort);

            var singleVNodeSettings = new SingleVNodeSettings(TcpEndPoint, HttpEndPoint, new[] {HttpEndPoint.ToHttpUrl()});
            var appSettings = new SingleVNodeAppSettings(TimeSpan.FromHours(1));

            _node = new SingleVNode(_tfChunkDb, singleVNodeSettings, appSettings, dbVerifyHashes: true);
        }

        public void Start()
        {
            _node.Start();
            Thread.Sleep(750);
        }

        public void Shutdown()
        {
            _node.Stop();
            Thread.Sleep(750);

            _chaserChk.Dispose();
            _writerChk.Dispose();
            _tfChunkDb.Dispose();

            TryDeleteDirectory(_oneTimeDbPath);
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

        private TFChunkDbConfig CreateOneTimeDbConfig(int chunkSize, string dbPath, int chunksToCache)
        {
            if (Runtime.IsMono)
            {
                _writerChk = new FileCheckpoint(Path.Combine(dbPath, Checkpoint.Writer + ".chk"), Checkpoint.Writer,
                                                cached: true);
                _chaserChk = new FileCheckpoint(Path.Combine(dbPath, Checkpoint.Chaser + ".chk"), Checkpoint.Chaser,
                                                cached: true);
            }
            else
            {
                _writerChk = new MemoryMappedFileCheckpoint(Path.Combine(dbPath, Checkpoint.Writer + ".chk"),
                                                            Checkpoint.Writer, cached: true);
                _chaserChk = new MemoryMappedFileCheckpoint(Path.Combine(dbPath, Checkpoint.Chaser + ".chk"),
                                                            Checkpoint.Chaser, cached: true);
            }
            var nodeConfig = new TFChunkDbConfig(dbPath,
                                                 new VersionedPatternFileNamingStrategy(dbPath, "chunk-"),
                                                 chunkSize,
                                                 chunksToCache,
                                                 _writerChk,
                                                 new[] {_chaserChk});

            return nodeConfig;
        }
    }
}
