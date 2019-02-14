using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Cluster.Settings;
using EventStore.Core.Messages;
using EventStore.Core.Services.Gossip;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Tests.Http;
using EventStore.Core.Tests.Services.Transport.Tcp;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Util;
using EventStore.Core.Data;

namespace EventStore.Core.Tests.Helpers {
	public class MiniClusterNode {
		public static int RunCount = 0;
		public static readonly Stopwatch RunningTime = new Stopwatch();
		public static readonly Stopwatch StartingTime = new Stopwatch();
		public static readonly Stopwatch StoppingTime = new Stopwatch();

		public const int ChunkSize = 1024 * 1024;
		public const int CachedChunkSize = ChunkSize + ChunkHeader.Size + ChunkFooter.Size;

		private static readonly ILogger Log = LogManager.GetLoggerFor<MiniClusterNode>();

		public IPEndPoint InternalTcpEndPoint { get; private set; }
		public IPEndPoint InternalTcpSecEndPoint { get; private set; }
		public IPEndPoint InternalHttpEndPoint { get; private set; }
		public IPEndPoint ExternalTcpEndPoint { get; private set; }
		public IPEndPoint ExternalTcpSecEndPoint { get; private set; }
		public IPEndPoint ExternalHttpEndPoint { get; private set; }

		public readonly int DebugIndex;

		public readonly ClusterVNode Node;
		public readonly TFChunkDb Db;
		private readonly string _dbPath;
		public ManualResetEvent StartedEvent;

		public VNodeState NodeState = VNodeState.Unknown;

		public MiniClusterNode(
			string pathname, int debugIndex, IPEndPoint internalTcp, IPEndPoint internalTcpSec, IPEndPoint internalHttp,
			IPEndPoint externalTcp, IPEndPoint externalTcpSec, IPEndPoint externalHttp, IPEndPoint[] gossipSeeds,
			ISubsystem[] subsystems = null, int? chunkSize = null, int? cachedChunkSize = null,
			bool enableTrustedAuth = false, bool skipInitializeStandardUsersCheck = true, int memTableSize = 1000,
			bool inMemDb = true, bool disableFlushToDisk = false) {
			RunningTime.Start();
			RunCount += 1;

			DebugIndex = debugIndex;

			_dbPath = Path.Combine(
				pathname,
				string.Format(
					"mini-cluster-node-db-{0}-{1}-{2}", externalTcp.Port, externalTcpSec.Port, externalHttp.Port));

			Directory.CreateDirectory(_dbPath);
			FileStreamExtensions.ConfigureFlush(disableFlushToDisk);
			Db =
				new TFChunkDb(
					CreateDbConfig(chunkSize ?? ChunkSize, _dbPath, cachedChunkSize ?? CachedChunkSize, inMemDb));

			InternalTcpEndPoint = internalTcp;
			InternalTcpSecEndPoint = internalTcpSec;
			InternalHttpEndPoint = internalHttp;

			ExternalTcpEndPoint = externalTcp;
			ExternalTcpSecEndPoint = externalTcpSec;
			ExternalHttpEndPoint = externalHttp;

			var singleVNodeSettings = new ClusterVNodeSettings(
				Guid.NewGuid(), debugIndex, InternalTcpEndPoint, InternalTcpSecEndPoint, ExternalTcpEndPoint,
				ExternalTcpSecEndPoint, InternalHttpEndPoint, ExternalHttpEndPoint,
				new Data.GossipAdvertiseInfo(InternalTcpEndPoint, InternalTcpSecEndPoint,
					ExternalTcpEndPoint, ExternalTcpSecEndPoint,
					InternalHttpEndPoint, ExternalHttpEndPoint,
					null, null, 0, 0),
				new[] {InternalHttpEndPoint.ToHttpUrl(EndpointExtensions.HTTP_SCHEMA)},
				new[] {ExternalHttpEndPoint.ToHttpUrl(EndpointExtensions.HTTP_SCHEMA)}, enableTrustedAuth,
				ssl_connections.GetCertificate(), 1, false,
				"", gossipSeeds, TFConsts.MinFlushDelayMs, 3, 2, 2, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10),
				false, false, "", false, TimeSpan.FromHours(1), StatsStorage.None, 0,
				new InternalAuthenticationProviderFactory(), disableScavengeMerging: true, scavengeHistoryMaxAge: 30,
				adminOnPublic: true,
				statsOnPublic: true, gossipOnPublic: true, gossipInterval: TimeSpan.FromSeconds(1),
				gossipAllowedTimeDifference: TimeSpan.FromSeconds(1), gossipTimeout: TimeSpan.FromSeconds(1),
				extTcpHeartbeatTimeout: TimeSpan.FromSeconds(2), extTcpHeartbeatInterval: TimeSpan.FromSeconds(2),
				intTcpHeartbeatTimeout: TimeSpan.FromSeconds(2), intTcpHeartbeatInterval: TimeSpan.FromSeconds(2),
				verifyDbHash: false, maxMemtableEntryCount: memTableSize,
				hashCollisionReadLimit: Opts.HashCollisionReadLimitDefault,
				startStandardProjections: false, disableHTTPCaching: false, logHttpRequests: false,
				connectionPendingSendBytesThreshold: Opts.ConnectionPendingSendBytesThresholdDefault,
				chunkInitialReaderCount: Opts.ChunkInitialReaderCountDefault);

			Log.Info(
				"\n{0,-25} {1} ({2}/{3}, {4})\n" + "{5,-25} {6} ({7})\n" + "{8,-25} {9} ({10}-bit)\n"
				+ "{11,-25} {12}\n" + "{13,-25} {14}\n" + "{15,-25} {16}\n" + "{17,-25} {18}\n" + "{19,-25} {20}\n\n",
				"ES VERSION:", VersionInfo.Version, VersionInfo.Branch, VersionInfo.Hashtag, VersionInfo.Timestamp,
				"OS:", OS.OsFlavor, Environment.OSVersion, "RUNTIME:", OS.GetRuntimeVersion(),
				Marshal.SizeOf(typeof(IntPtr)) * 8, "GC:",
				GC.MaxGeneration == 0
					? "NON-GENERATION (PROBABLY BOEHM)"
					: string.Format("{0} GENERATIONS", GC.MaxGeneration + 1), "DBPATH:", _dbPath, "ExTCP ENDPOINT:",
				ExternalTcpEndPoint, "ExTCP SECURE ENDPOINT:", ExternalTcpSecEndPoint, "ExHTTP ENDPOINT:",
				ExternalHttpEndPoint);

			Node = new ClusterVNode(Db, singleVNodeSettings,
				infoController: new InfoController(null, ProjectionType.None), subsystems: subsystems,
				gossipSeedSource: new KnownEndpointGossipSeedSource(gossipSeeds));
			Node.ExternalHttpService.SetupController(new TestController(Node.MainQueue));
		}

		public void Start() {
			StartingTime.Start();

			StartedEvent = new ManualResetEvent(false);
			Node.MainBus.Subscribe(
				new AdHocHandler<SystemMessage.StateChangeMessage>(m => { NodeState = VNodeState.Unknown; }));
			Node.MainBus.Subscribe(
				new AdHocHandler<SystemMessage.BecomeMaster>(m => {
					NodeState = VNodeState.Master;
					StartedEvent.Set();
				}));
			Node.MainBus.Subscribe(
				new AdHocHandler<SystemMessage.BecomeSlave>(m => {
					NodeState = VNodeState.Slave;
					StartedEvent.Set();
				}));

			Node.Start();
		}

		public void Shutdown(bool keepDb = false, bool keepPorts = false) {
			StoppingTime.Start();
			if (!Node.Stop(TimeSpan.FromSeconds(20), false, true))
				throw new TimeoutException("MiniNode has not shut down in 20 seconds.");

			if (!keepPorts) {
				PortsHelper.ReturnPort(InternalTcpEndPoint.Port);
				PortsHelper.ReturnPort(InternalTcpSecEndPoint.Port);
				PortsHelper.ReturnPort(InternalHttpEndPoint.Port);
				PortsHelper.ReturnPort(ExternalTcpEndPoint.Port);
				PortsHelper.ReturnPort(ExternalTcpSecEndPoint.Port);
				PortsHelper.ReturnPort(ExternalHttpEndPoint.Port);
			}

			if (!keepDb)
				TryDeleteDirectory(_dbPath);

			StoppingTime.Stop();
			RunningTime.Stop();
		}

		private void TryDeleteDirectory(string directory) {
			try {
				Directory.Delete(directory, true);
			} catch (Exception e) {
				Debug.WriteLine("Failed to remove directory {0}", directory);
				Debug.WriteLine(e);
			}
		}

		private TFChunkDbConfig CreateDbConfig(int chunkSize, string dbPath, long chunksCacheSize, bool inMemDb) {
			ICheckpoint writerChk;
			ICheckpoint chaserChk;
			ICheckpoint epochChk;
			ICheckpoint truncateChk;
			ICheckpoint replicationCheckpoint = new InMemoryCheckpoint(-1);
			if (inMemDb) {
				writerChk = new InMemoryCheckpoint(Checkpoint.Writer);
				chaserChk = new InMemoryCheckpoint(Checkpoint.Chaser);
				epochChk = new InMemoryCheckpoint(Checkpoint.Epoch, initValue: -1);
				truncateChk = new InMemoryCheckpoint(Checkpoint.Truncate, initValue: -1);
			} else {
				var writerCheckFilename = Path.Combine(dbPath, Checkpoint.Writer + ".chk");
				var chaserCheckFilename = Path.Combine(dbPath, Checkpoint.Chaser + ".chk");
				var epochCheckFilename = Path.Combine(dbPath, Checkpoint.Epoch + ".chk");
				var truncateCheckFilename = Path.Combine(dbPath, Checkpoint.Truncate + ".chk");
				if (Runtime.IsMono) {
					writerChk = new FileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
					chaserChk = new FileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);
					epochChk = new FileCheckpoint(epochCheckFilename, Checkpoint.Epoch, cached: true, initValue: -1);
					truncateChk = new FileCheckpoint(
						truncateCheckFilename, Checkpoint.Truncate, cached: true, initValue: -1);
				} else {
					writerChk = new MemoryMappedFileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
					chaserChk = new MemoryMappedFileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);
					epochChk = new MemoryMappedFileCheckpoint(
						epochCheckFilename, Checkpoint.Epoch, cached: true, initValue: -1);
					truncateChk = new MemoryMappedFileCheckpoint(
						truncateCheckFilename, Checkpoint.Truncate, cached: true, initValue: -1);
				}
			}

			var nodeConfig = new TFChunkDbConfig(
				dbPath, new VersionedPatternFileNamingStrategy(dbPath, "chunk-"), chunkSize, chunksCacheSize, writerChk,
				chaserChk, epochChk, truncateChk, replicationCheckpoint, Opts.ChunkInitialReaderCountDefault, inMemDb);
			return nodeConfig;
		}
	}
}
