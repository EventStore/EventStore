using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClusterNode;
using EventStore.Core;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public partial class EventStoreClientAPIFixture : IAsyncLifetime {
		private const string TestEventType = "-";
		private const int PortStart = 1024;
		private const int PortCount = 32768 - PortStart;

		private static readonly X509Certificate2 ServerCertificate;
		private static readonly Queue<int> AvailablePorts =
			new Queue<int>(Enumerable.Range(PortStart, PortCount));


		public static readonly int ExternalPort;
		public static readonly int ExternalSecurePort;
		public static readonly int UnusedPort;
		private readonly ClusterVNode _node;
		public IDictionary<bool, IEventStoreConnection> Connections { get; }

		static EventStoreClientAPIFixture() {
			using var stream = typeof(EventStoreClientAPIFixture)
				.Assembly
				.GetManifestResourceStream(typeof(EventStoreClientAPIFixture), "server.p12");
			using var mem = new MemoryStream();
			stream.CopyTo(mem);
			ServerCertificate = new X509Certificate2(mem.ToArray(), "1111");

			var defaultLoopBack = new IPEndPoint(IPAddress.Loopback, 0);

			ExternalPort = GetFreePort();
			ExternalSecurePort = GetFreePort();
			UnusedPort = GetFreePort();

			int GetFreePort() {
				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
					return GetFreePortMacOS();
				}
				using var socket =
					new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) {
						ExclusiveAddressUse = false
					};
				socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				socket.Bind(defaultLoopBack);
				return ((IPEndPoint)socket.LocalEndPoint).Port;
			}

			int GetFreePortMacOS() {
				const int maxAttempts = 50;

				var properties = IPGlobalProperties.GetIPGlobalProperties();

				var ipEndPoints = properties.GetActiveTcpConnections()
					.Select(x => x.LocalEndPoint)
					.Concat(properties.GetActiveTcpListeners())
					.Concat(properties.GetActiveUdpListeners())
					.Where(x => x.AddressFamily == AddressFamily.InterNetwork &&
					            x.Address.Equals(IPAddress.Loopback) &&
					            x.Port >= PortStart &&
					            x.Port < PortStart + PortCount)
					.OrderBy(x => x.Port)
					.ToArray();
				var inUse = new HashSet<int>(ipEndPoints.Select(x => x.Port));

				var attempt = 0;

				while (attempt++ < maxAttempts && AvailablePorts.TryDequeue(out var port)) {
					using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream,
						ProtocolType.Tcp);
					try {
						socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
						return port;
					} catch (Exception) {
						// ignored
					}
				}

				throw new Exception(
					$"Could not find free port on {IPAddress.Loopback} after {attempt} attempts. The following ports are used: {string.Join(",", inUse)}");
			}
		}

		public EventStoreClientAPIFixture() {
			var vNodeBuilder = ClusterVNodeBuilder
				.AsSingleNode()
				.WithExternalTcpOn(new IPEndPoint(IPAddress.Loopback, ExternalPort))
				.WithExternalSecureTcpOn(new IPEndPoint(IPAddress.Loopback, ExternalSecurePort))
				.WithServerCertificate(ServerCertificate)
				.RunInMemory();

			_node = vNodeBuilder.Build();
			Connections = new Dictionary<bool, IEventStoreConnection> {
				[false] = CreateConnection(settings => settings.UseSsl(false), ExternalPort),
				[true] = CreateConnection(settings => settings.UseSsl(true), ExternalSecurePort)
			};
		}

		public async Task InitializeAsync() {
			await _node.StartAndWaitUntilReady();
			await Connections[true].ConnectAsync();
			await Connections[false].ConnectAsync();
		}

		public Task DisposeAsync() {
			Connections[true].Dispose();
			Connections[false].Dispose();
			return _node.Stop().WithTimeout();
		}

		private ConnectionSettingsBuilder DefaultBuilder {
			get {
				var builder = ConnectionSettings.Create()
					.EnableVerboseLogging()
					.LimitReconnectionsTo(10)
					.LimitRetriesForOperationTo(100)
					.SetTimeoutCheckPeriodTo(TimeSpan.FromMilliseconds(100))
					.SetReconnectionDelayTo(TimeSpan.Zero)
					.FailOnNoServerResponse();

				// ReSharper disable ConditionIsAlwaysTrueOrFalse
				// ReSharper disable HeuristicUnreachableCode
				#if DEBUG
				if (UseLoggerBridge) {
					builder = builder.UseCustomLogger(ConsoleLoggerBridge.Default);
				}
				// ReSharper restore HeuristicUnreachableCode
				// ReSharper restore ConditionIsAlwaysTrueOrFalse
				#endif
				return builder;
			}
		}

		private static ConnectionSettingsBuilder DefaultConfigureSettings(
			ConnectionSettingsBuilder builder)
			=> builder;

		public IEnumerable<EventData> CreateTestEvents(int count = 1)
			=> Enumerable.Range(0, count).Select(CreateTestEvent);

		protected static EventData CreateTestEvent(int index) =>
			new EventData(Guid.NewGuid(), TestEventType, true, Encoding.UTF8.GetBytes($@"{{""x"":{index}}}"),
				Array.Empty<byte>());
	}
}
