using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using EventStore.Common.Utils;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Tests.Http;
using EventStore.Core.Tests.Services.Transport.Tcp;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Tests.Common.VNodeBuilderTests;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Tests.Helpers {
	public class MiniNode {
		public static int RunCount;
		public static readonly Stopwatch RunningTime = new Stopwatch();
		public static readonly Stopwatch StartingTime = new Stopwatch();
		public static readonly Stopwatch StoppingTime = new Stopwatch();

		public const int ChunkSize = 1024 * 1024;
		public const int CachedChunkSize = ChunkSize + ChunkHeader.Size + ChunkFooter.Size;

		private static readonly ILogger Log = Serilog.Log.ForContext<MiniNode>();

		public IPEndPoint TcpEndPoint { get; private set; }
		public IPEndPoint TcpSecEndPoint { get; private set; }
		public IPEndPoint IntTcpEndPoint { get; private set; }
		public IPEndPoint IntSecTcpEndPoint { get; private set; }
		public IPEndPoint HttpEndPoint { get; private set; }
		public readonly ClusterVNode Node;
		public readonly TFChunkDb Db;
		public readonly string DbPath;
		public readonly HttpClient HttpClient;
		public readonly HttpMessageHandler HttpMessageHandler;

		private readonly TestServer _kestrelTestServer;
		private readonly TaskCompletionSource<bool> _started;
		private readonly TaskCompletionSource<bool> _adminUserCreated;

		public Task Started => _started.Task;
		public Task AdminUserCreated => _adminUserCreated.Task;

		public MiniNode(string pathname,
			int? tcpPort = null, int? tcpSecPort = null, int? httpPort = null,
			ISubsystem[] subsystems = null,
			int? chunkSize = null, int? cachedChunkSize = null, bool enableTrustedAuth = false,
			bool skipInitializeStandardUsersCheck = true,
			int memTableSize = 1000,
			bool inMemDb = true, bool disableFlushToDisk = false,
			string advertisedExtHostAddress = null, int advertisedHttpPort = 0,
			int hashCollisionReadLimit = EventStore.Core.Util.Opts.HashCollisionReadLimitDefault,
			byte indexBitnessVersion = EventStore.Core.Util.Opts.IndexBitnessVersionDefault,
			string dbPath = "", bool isReadOnlyReplica = false) {
			RunningTime.Start();
			RunCount += 1;

			var ip = IPAddress.Loopback; //GetLocalIp();
			

			int extTcpPort = tcpPort ?? PortsHelper.GetAvailablePort(ip);
			int extSecTcpPort = tcpSecPort ?? PortsHelper.GetAvailablePort(ip);
			int httpEndPointPort = httpPort ?? PortsHelper.GetAvailablePort(ip);
			int intTcpPort = PortsHelper.GetAvailablePort(ip);
			int intSecTcpPort = PortsHelper.GetAvailablePort(ip);

			if (string.IsNullOrEmpty(dbPath)) {
				DbPath = Path.Combine(pathname,
					$"mini-node-db-{extTcpPort}-{extSecTcpPort}-{httpEndPointPort}");
			} else {
				DbPath = dbPath;
			}

			TcpEndPoint = new IPEndPoint(ip, extTcpPort);
			TcpSecEndPoint = new IPEndPoint(ip, extSecTcpPort);
			IntTcpEndPoint = new IPEndPoint(ip, intTcpPort);
			IntSecTcpEndPoint = new IPEndPoint(ip, intSecTcpPort);
			HttpEndPoint = new IPEndPoint(ip, httpEndPointPort);

			var builder = TestVNodeBuilder.AsSingleNode();
			if (inMemDb)
				builder.RunInMemory();
			else
				builder.RunOnDisk(DbPath);

			builder.WithInternalTcpOn(IntTcpEndPoint)
				.WithInternalSecureTcpOn(IntSecTcpEndPoint)
				.WithExternalTcpOn(TcpEndPoint)
				.WithExternalSecureTcpOn(TcpSecEndPoint)
				.WithHttpOn(HttpEndPoint)
				.WithTfChunkSize(chunkSize ?? ChunkSize)
				.WithTfChunksCacheSize(cachedChunkSize ?? CachedChunkSize)
				.WithServerCertificate(ssl_connections.GetServerCertificate())
				.WithWorkerThreads(1)
				.DisableDnsDiscovery()
				.WithPrepareTimeout(TimeSpan.FromSeconds(10))
				.WithCommitTimeout(TimeSpan.FromSeconds(10))
				.WithStatsPeriod(TimeSpan.FromHours(1))
				.DisableScavengeMerging()
				.WithInternalHeartbeatInterval(TimeSpan.FromSeconds(10))
				.WithInternalHeartbeatTimeout(TimeSpan.FromSeconds(10))
				.WithExternalHeartbeatInterval(TimeSpan.FromSeconds(10))
				.WithExternalHeartbeatTimeout(TimeSpan.FromSeconds(10))
				.MaximumMemoryTableSizeOf(memTableSize)
				.DoNotVerifyDbHashes()
				.WithStatsStorage(StatsStorage.None)
				.AdvertiseExternalHostAs(advertisedExtHostAddress)
				.AdvertiseHttpPortAs(advertisedHttpPort)
				.WithHashCollisionReadLimitOf(hashCollisionReadLimit)
				.WithIndexBitnessVersion(indexBitnessVersion)
				.EnableExternalTCP()
				.WithEnableAtomPubOverHTTP(true);

			if (enableTrustedAuth)
				builder.EnableTrustedAuth();
			if (disableFlushToDisk)
				builder.WithUnsafeDisableFlushToDisk();
			if (isReadOnlyReplica)
				builder.EnableReadOnlyReplica();

			if (subsystems != null) {
				foreach (var subsystem in subsystems) {
					builder.AddCustomSubsystem(subsystem);
				}
			}

			Log.Information("\n{0,-25} {1} ({2}/{3}, {4})\n"
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
				"HTTP ENDPOINT:", HttpEndPoint);

			Node = builder.Build();
			Db = builder.GetDb();

			Node.HttpService.SetupController(new TestController(Node.MainQueue));
			_kestrelTestServer = new TestServer(new WebHostBuilder()
				.UseKestrel()
				.UseStartup(Node.Startup));
			_started = new TaskCompletionSource<bool>();
			_adminUserCreated = new TaskCompletionSource<bool>();
			HttpMessageHandler = _kestrelTestServer.CreateHandler();
			HttpClient = new HttpClient(HttpMessageHandler) {
				BaseAddress = new UriBuilder {
					Scheme = Uri.UriSchemeHttps
				}.Uri
			};
		}

		public async Task Start() {
			StartingTime.Start();
			Node.MainBus.Subscribe(
				new AdHocHandler<SystemMessage.BecomeLeader>(m => {
					_started.TrySetResult(true);
				}));

			AdHocHandler<StorageMessage.EventCommitted> waitForAdminUser = null;
			waitForAdminUser = new AdHocHandler<StorageMessage.EventCommitted>(WaitForAdminUser);
			Node.MainBus.Subscribe(waitForAdminUser);

			void WaitForAdminUser(StorageMessage.EventCommitted m) {
				if (m.Event.EventStreamId != "$user-admin") {
					return;
				}

				_adminUserCreated.TrySetResult(true);
				Node.MainBus.Unsubscribe(waitForAdminUser);
			}

			await Node.StartAsync(true).WithTimeout(TimeSpan.FromSeconds(60))
				.ConfigureAwait(false); //starts the node

			StartingTime.Stop();
			Log.Information("MiniNode successfully started!");
		}

		public async Task Shutdown(bool keepDb = false) {

			StoppingTime.Start();

			_kestrelTestServer.Dispose();
			HttpMessageHandler.Dispose();
			HttpClient.Dispose();
			await Node.StopAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(false);

			if (!keepDb)
				TryDeleteDirectory(DbPath);

			StoppingTime.Stop();
			RunningTime.Stop();

		}

		public void WaitIdle() {
#if DEBUG
			Node.QueueStatsManager.WaitIdle();
#endif
		}

		private void TryDeleteDirectory(string directory) {
			try {
				Directory.Delete(directory, true);
			} catch (Exception e) {
				Debug.WriteLine("Failed to remove directory {0}", directory);
				Debug.WriteLine(e);
			}
		}
	}
}
