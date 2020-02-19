using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using EventStore.Common.Utils;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Tests.Http;
using EventStore.Core.Tests.Services.Transport.Tcp;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Tests.Common.VNodeBuilderTests;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
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
		public IPEndPoint IntHttpEndPoint { get; private set; }
		public IPEndPoint IntTcpEndPoint { get; private set; }
		public IPEndPoint IntSecTcpEndPoint { get; private set; }
		public IPEndPoint ExtHttpEndPoint { get; private set; }
		public readonly ClusterVNode Node;
		public readonly TFChunkDb Db;
		public readonly string DbPath;
		public readonly HttpClient HttpClient;
		public readonly HttpMessageHandler HttpMessageHandler;

		private readonly TestServer _kestrelTestServer;

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
			string dbPath = "", bool isReadOnlyReplica = false) {
			RunningTime.Start();
			RunCount += 1;

			var ip = IPAddress.Loopback; //GetLocalIp();
			

			int extTcpPort = tcpPort ?? PortsHelper.GetAvailablePort(ip);
			int extSecTcpPort = tcpSecPort ?? PortsHelper.GetAvailablePort(ip);
			int extHttpPort = httpPort ?? PortsHelper.GetAvailablePort(ip);
			int intTcpPort = PortsHelper.GetAvailablePort(ip);
			int intSecTcpPort = PortsHelper.GetAvailablePort(ip);
			int intHttpPort = PortsHelper.GetAvailablePort(ip);

			if (string.IsNullOrEmpty(dbPath)) {
				DbPath = Path.Combine(pathname,
					$"mini-node-db-{extTcpPort}-{extSecTcpPort}-{extHttpPort}");
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
				.WithIndexBitnessVersion(indexBitnessVersion)
				.WithHttpMessageHandlerFactory(() => HttpMessageHandler);

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
				"HTTP ENDPOINT:", ExtHttpEndPoint);

			Node = builder.Build();
			Db = ((TestVNodeBuilder)builder).GetDb();

			_kestrelTestServer = new TestServer(new WebHostBuilder()
				.UseKestrel()
				.UseStartup(Node.Startup));
			HttpMessageHandler = _kestrelTestServer.CreateHandler();
			HttpClient = new HttpClient(HttpMessageHandler) {
				BaseAddress = new UriBuilder {
					Scheme = Uri.UriSchemeHttps
				}.Uri
			};

			Node.ExternalHttpService.SetupController(new TestController(Node.MainQueue));
		}

		public async Task Start() {
			StartingTime.Start();

			await Node.StartAsync(true).WithTimeout(TimeSpan.FromSeconds(60))
				.ConfigureAwait(false); //starts the node

			StartingTime.Stop();
			Log.Information("MiniNode successfully started!");
		}

		private async Task StartMiniNode(Task monitorFailuresTask) {
			StartingTime.Start();

			var startNodeTask = Node.StartAsync(true); //starts the node

			await Task.WhenAny(
				monitorFailuresTask,
				startNodeTask
			).WithTimeout(TimeSpan.FromSeconds(60)); //startup timeout of 60s
			StartingTime.Stop();
			Log.Information("MiniNode successfully started!");
		}

		public void MonitorFailures(TaskCompletionSource<object> tcs) {
			if (tcs.Task.IsCompleted)
				return;
			if (!Node.Tasks.Any())
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

		private IPAddress GetLocalIp() {
			var host = Dns.GetHostEntry(Dns.GetHostName());
			return host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
		}
	}
}
