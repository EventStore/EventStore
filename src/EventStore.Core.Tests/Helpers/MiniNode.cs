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
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Authentication;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Settings;
using EventStore.Core.Tests.Http;
using EventStore.Core.Tests.Services.Transport.Tcp;
using EventStore.Core.Services.Gossip;
using EventStore.Core.Cluster.Settings;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Tests.Common.VNodeBuilderTests;
using System.Threading.Tasks;

namespace EventStore.Core.Tests.Helpers {
	public class MiniNode {
		private static bool _running;

		public static int RunCount = 0;
		public static readonly Stopwatch RunningTime = new Stopwatch();
		public static readonly Stopwatch StartingTime = new Stopwatch();
		public static readonly Stopwatch StoppingTime = new Stopwatch();

		public const int ChunkSize = 1024 * 1024;
		public const int CachedChunkSize = ChunkSize + ChunkHeader.Size + ChunkFooter.Size;

		private static readonly ILogger Log = LogManager.GetLoggerFor<MiniNode>();

		public IPEndPoint TcpEndPoint { get; private set; }
		public IPEndPoint TcpSecEndPoint { get; private set; }
		public IPEndPoint IntHttpEndPoint { get; private set; }
		public IPEndPoint IntTcpEndPoint { get; private set; }
		public IPEndPoint IntSecTcpEndPoint { get; private set; }
		public IPEndPoint ExtHttpEndPoint { get; private set; }
		public readonly ClusterVNode Node;
		public readonly TFChunkDb Db;
		public readonly string DbPath;

		public MiniNode(string pathname,
			int? tcpPort = null, int? tcpSecPort = null, int? httpPort = null,
			ISubsystem[] subsystems = null,
			int? chunkSize = null, int? cachedChunkSize = null, bool enableTrustedAuth = false,
			bool skipInitializeStandardUsersCheck = true,
			int memTableSize = 1000,
			bool inMemDb = true, bool disableFlushToDisk = false,
			IPAddress advertisedExtIPAddress = null, int advertisedExtHttpPort = 0,
			int hashCollisionReadLimit = EventStore.Core.Util.Opts.HashCollisionReadLimitDefault,
			byte indexBitnessVersion = EventStore.Core.Util.Opts.IndexBitnessVersionDefault,
			string dbPath = "") {
			if (_running) throw new Exception("Previous MiniNode is still running!!!");
			_running = true;

			RunningTime.Start();
			RunCount += 1;

			IPAddress ip = IPAddress.Loopback; //GetLocalIp();

			int extTcpPort = tcpPort ?? PortsHelper.GetAvailablePort(ip);
			int extSecTcpPort = tcpSecPort ?? PortsHelper.GetAvailablePort(ip);
			int extHttpPort = httpPort ?? PortsHelper.GetAvailablePort(ip);
			int intTcpPort = PortsHelper.GetAvailablePort(ip);
			int intSecTcpPort = PortsHelper.GetAvailablePort(ip);
			int intHttpPort = PortsHelper.GetAvailablePort(ip);

			if (String.IsNullOrEmpty(dbPath)) {
				DbPath = Path.Combine(pathname,
					string.Format("mini-node-db-{0}-{1}-{2}", extTcpPort, extSecTcpPort, extHttpPort));
			} else {
				DbPath = dbPath;
			}

			TcpEndPoint = new IPEndPoint(ip, extTcpPort);
			TcpSecEndPoint = new IPEndPoint(ip, extSecTcpPort);
			IntTcpEndPoint = new IPEndPoint(ip, intTcpPort);
			IntSecTcpEndPoint = new IPEndPoint(ip, intSecTcpPort);
			IntHttpEndPoint = new IPEndPoint(ip, intHttpPort);
			ExtHttpEndPoint = new IPEndPoint(ip, extHttpPort);

			var builder = TestVNodeBuilder.AsSingleNode();
			if (inMemDb)
				builder.RunInMemory();
			else
				builder.RunOnDisk(DbPath);

			builder.WithInternalTcpOn(IntTcpEndPoint)
				.WithInternalSecureTcpOn(IntSecTcpEndPoint)
				.WithExternalTcpOn(TcpEndPoint)
				.WithExternalSecureTcpOn(TcpSecEndPoint)
				.WithInternalHttpOn(IntHttpEndPoint)
				.WithExternalHttpOn(ExtHttpEndPoint)
				.WithTfChunkSize(chunkSize ?? ChunkSize)
				.WithTfChunksCacheSize(cachedChunkSize ?? CachedChunkSize)
				.WithServerCertificate(ssl_connections.GetCertificate())
				.WithWorkerThreads(1)
				.DisableDnsDiscovery()
				.WithPrepareTimeout(TimeSpan.FromSeconds(10))
				.WithCommitTimeout(TimeSpan.FromSeconds(10))
				.WithStatsPeriod(TimeSpan.FromHours(1))
				.DisableScavengeMerging()
				.NoGossipOnPublicInterface()
				.WithInternalHeartbeatInterval(TimeSpan.FromSeconds(10))
				.WithInternalHeartbeatTimeout(TimeSpan.FromSeconds(10))
				.WithExternalHeartbeatInterval(TimeSpan.FromSeconds(10))
				.WithExternalHeartbeatTimeout(TimeSpan.FromSeconds(10))
				.MaximumMemoryTableSizeOf(memTableSize)
				.DoNotVerifyDbHashes()
				.WithStatsStorage(StatsStorage.None)
				.AdvertiseExternalIPAs(advertisedExtIPAddress)
				.AdvertiseExternalHttpPortAs(advertisedExtHttpPort)
				.WithHashCollisionReadLimitOf(hashCollisionReadLimit)
				.WithIndexBitnessVersion(indexBitnessVersion);

			if (enableTrustedAuth)
				builder.EnableTrustedAuth();
			if (disableFlushToDisk)
				builder.WithUnsafeDisableFlushToDisk();

			if (subsystems != null) {
				foreach (var subsystem in subsystems) {
					builder.AddCustomSubsystem(subsystem);
				}
			}

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
				"GC:",
				GC.MaxGeneration == 0
					? "NON-GENERATION (PROBABLY BOEHM)"
					: string.Format("{0} GENERATIONS", GC.MaxGeneration + 1),
				"DBPATH:", DbPath,
				"TCP ENDPOINT:", TcpEndPoint,
				"TCP SECURE ENDPOINT:", TcpSecEndPoint,
				"HTTP ENDPOINT:", ExtHttpEndPoint);

			Node = builder.Build();
			Db = ((TestVNodeBuilder)builder).GetDb();

			Node.ExternalHttpService.SetupController(new TestController(Node.MainQueue));
		}

		public void Start() {
			var monitorTcs = new TaskCompletionSource<object>();
			MonitorFailures(monitorTcs);
			StartMiniNode(monitorTcs.Task).Wait();
			ContinueMonitoringFailures(monitorTcs);
		}

		private Task StartMiniNode(Task monitorFailuresTask) {
			StartingTime.Start();

			var startNodeTask = Node.StartAndWaitUntilReady(); //starts the node
			var startupTimeoutTask = Task.Delay(TimeSpan.FromSeconds(60)); //startup timeout of 60s

			return Task.WhenAny(
				monitorFailuresTask,
				startNodeTask,
				startupTimeoutTask
			).ContinueWith((t) => {
				StartingTime.Stop();
				if (monitorFailuresTask.IsCompleted) {
					if (monitorFailuresTask.Exception != null)
						throw monitorFailuresTask.Exception;
					else {
						throw new ApplicationException(
							"Monitor Failures task completed but no exceptions were thrown.");
					}
				} else if (startupTimeoutTask.IsCompleted) {
					throw new TimeoutException("MiniNode has not started in 60 seconds.");
				} else if (startNodeTask.IsCompleted) {
					if (t.Status != TaskStatus.RanToCompletion)
						throw new ApplicationException("MiniNode has not properly started.");
					Log.Info("MiniNode successfully started!");
				}
			});
		}

		public void MonitorFailures(TaskCompletionSource<object> tcs) {
			if (tcs.Task.IsCompleted)
				return;
			if (Node.Tasks.Count() == 0)
				return;

			Task.WhenAny(Node.Tasks)
				.ContinueWith((t) => {
					if (tcs.Task.IsCompleted)
						return;

					if (t.Result.Exception != null)
						tcs.TrySetException(t.Result.Exception);
					else
						MonitorFailures(tcs);
				});
		}

		public void ContinueMonitoringFailures(TaskCompletionSource<object> tcs) {
			if (tcs.Task.IsCompleted)
				return;
			tcs.Task.ContinueWith((t) => {
				if (t.Exception != null)
					throw t.Exception;
				else
					throw new ApplicationException("Node Monitor task completed but no exceptions were thrown.");
			});
		}

		public void Shutdown(bool keepDb = false, bool keepPorts = false) {
			StoppingTime.Start();

			if (!Node.Stop(TimeSpan.FromSeconds(20), true, true))
				throw new TimeoutException("MiniNode has not shut down in 20 seconds.");

			if (!keepPorts) {
				PortsHelper.ReturnPort(TcpEndPoint.Port);
				PortsHelper.ReturnPort(TcpSecEndPoint.Port);
				PortsHelper.ReturnPort(IntHttpEndPoint.Port);
				PortsHelper.ReturnPort(ExtHttpEndPoint.Port);
				PortsHelper.ReturnPort(IntTcpEndPoint.Port);
				PortsHelper.ReturnPort(IntSecTcpEndPoint.Port);
			}

			if (!keepDb)
				TryDeleteDirectory(DbPath);

			StoppingTime.Stop();
			RunningTime.Stop();

			_running = false;
		}

		private void TryDeleteDirectory(string directory) {
			try {
				Directory.Delete(directory, true);
			} catch (Exception e) {
				Debug.WriteLine("Failed to remove directory {0}", directory);
				Debug.WriteLine(e);
			}
		}

		private IPAddress GetLocalIp() {
			var host = Dns.GetHostEntry(Dns.GetHostName());
			return host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
		}
	}
}
