using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClusterNode;
using EventStore.Core;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public partial class EventStoreClientAPIFixture : IAsyncLifetime {
		private const string TestEventType = "-";

		private static readonly X509Certificate2 ServerCertificate;
		public static readonly int ExternalPort = PortHelper.GetAvailablePort(IPAddress.Loopback);
		public static readonly int ExternalSecurePort = PortHelper.GetAvailablePort(IPAddress.Loopback);
		public static readonly int UnusedPort = PortHelper.GetAvailablePort(IPAddress.Loopback);
		private readonly ClusterVNode _node;

		static EventStoreClientAPIFixture() {
			using var stream = typeof(EventStoreClientAPIFixture)
				.Assembly
				.GetManifestResourceStream(typeof(EventStoreClientAPIFixture), "server.p12");
			using var mem = new MemoryStream();
			stream.CopyTo(mem);
			ServerCertificate = new X509Certificate2(mem.ToArray(), "1111");
		}

		public EventStoreClientAPIFixture() {
			var vNodeBuilder = ClusterVNodeBuilder
				.AsSingleNode()
				.WithExternalTcpOn(new IPEndPoint(IPAddress.Loopback, ExternalPort))
				.WithExternalSecureTcpOn(new IPEndPoint(IPAddress.Loopback, ExternalSecurePort))
				.WithServerCertificate(ServerCertificate)
				.RunInMemory();

			_node = vNodeBuilder.Build();
		}

		public async Task InitializeAsync() {
			await _node.StartAndWaitUntilReady();
		}

		public async Task DisposeAsync() {
			await _node.Stop().WithTimeout();
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
				if (UseLoggerBridge) {
					builder = builder.UseCustomLogger(ConsoleLoggerBridge.Default);
				}
				// ReSharper restore ConditionIsAlwaysTrueOrFalse

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

		private static class PortHelper {
			private const int PortStart = 49152;
			private const int PortCount = ushort.MaxValue - PortStart;

			private static readonly ConcurrentQueue<int> AvailablePorts =
				new ConcurrentQueue<int>(Enumerable.Range(PortStart, PortCount));

			public static int GetAvailablePort(IPAddress ip) {
				var properties = IPGlobalProperties.GetIPGlobalProperties();

				var ipEndPoints = properties.GetActiveTcpConnections().Select(x => x.LocalEndPoint)
					.Concat(properties.GetActiveTcpListeners())
					.Concat(properties.GetActiveUdpListeners())
					.Where(x => x.AddressFamily == AddressFamily.InterNetwork &&
					            x.Address.Equals(ip) &&
					            x.Port >= PortStart &&
					            x.Port < PortStart + PortCount)
					.OrderBy(x => x.Port)
					.ToArray();

				var inUse = new HashSet<int>(ipEndPoints.Select(x => x.Port));

				while (AvailablePorts.TryDequeue(out var port)) {
					if (!inUse.Contains(port)) {
						return port;
					}
				}

				throw new InvalidOperationException("Ran out of ports.");
			}
		}
	}
}
