using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.ClusterNode;
using EventStore.Core;
using Xunit;
using Xunit.Abstractions;

namespace EventStore.ClientAPIAcceptanceTests {
	public partial class EventStoreClientAPIFixture : IAsyncLifetime {
		private const string TestEventType = "-";
		public static readonly int ExternalPort = PortHelper.GetAvailablePort(IPAddress.Loopback);
		private readonly ClusterVNode _node;

		public ITestOutputHelper TestOutputHelper { get; set; }

		public EventStoreClientAPIFixture() {
			var vNodeBuilder = ClusterVNodeBuilder
				.AsSingleNode()
				.WithExternalTcpOn(new IPEndPoint(IPAddress.Loopback, ExternalPort))
				.RunInMemory();

			_node = vNodeBuilder.Build();
		}

		public async Task InitializeAsync() {
			await _node.StartAndWaitUntilReady();
		}

		public async Task DisposeAsync() {
			await _node.Stop().WithTimeout();
		}

		public ConnectionSettingsBuilder Settings(bool useSsl = false, UserCredentials userCredentials = default) {
			var settings = ConnectionSettings.Create()
				.SetDefaultUserCredentials(userCredentials)
				//.UseCustomLogger(new LoggerBridge(TestOutputHelper))
				.EnableVerboseLogging()
				.LimitReconnectionsTo(10)
				.LimitRetriesForOperationTo(100)
				.SetTimeoutCheckPeriodTo(TimeSpan.FromMilliseconds(100))
				.SetReconnectionDelayTo(TimeSpan.Zero)
				.FailOnNoServerResponse();

			if (useSsl) {
				settings.UseSslConnection("ES", false);
			}

			return settings;
		}

		public IEnumerable<EventData> CreateTestEvents(int count = 1)
			=> Enumerable.Range(0, count).Select(CreateTestEvent);

		protected static EventData CreateTestEvent(int index) =>
			new EventData(Guid.NewGuid(), TestEventType, true, Encoding.UTF8.GetBytes($@"{{""x"":{index}}}"),
				Array.Empty<byte>());


		private static class PortHelper {
			private const int PortStart = 49152;
			private const int PortCount = ushort.MaxValue - PortStart;

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

				var useablePorts = new HashSet<int>(Enumerable.Range(PortStart, PortCount));

				useablePorts.ExceptWith(ipEndPoints.Select(x => x.Port));
				return useablePorts.FirstOrDefault();
			}
		}
	}
}
