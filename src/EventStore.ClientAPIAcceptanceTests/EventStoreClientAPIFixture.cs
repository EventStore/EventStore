using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
		public static readonly int ExternalPort;
		public static readonly int ExternalSecurePort;
		public static readonly int UnusedPort;
		private readonly ClusterVNode _node;

		static EventStoreClientAPIFixture() {
			using var stream = typeof(EventStoreClientAPIFixture)
				.Assembly
				.GetManifestResourceStream(typeof(EventStoreClientAPIFixture), "server.p12");
			using var mem = new MemoryStream();
			stream.CopyTo(mem);
			ServerCertificate = new X509Certificate2(mem.ToArray(), "1111");

			var defaultLoopBack = new IPEndPoint(IPAddress.Loopback, 0);

			using var external = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			using var externalSecure = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			using var unused = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			external.Bind(defaultLoopBack);
			externalSecure.Bind(defaultLoopBack);
			unused.Bind(defaultLoopBack);

			ExternalPort = ((IPEndPoint)external.LocalEndPoint).Port;
			ExternalSecurePort = ((IPEndPoint)externalSecure.LocalEndPoint).Port;
			UnusedPort = ((IPEndPoint)unused.LocalEndPoint).Port;
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

		public Task InitializeAsync() => _node.StartAndWaitUntilReady();

		public Task DisposeAsync() => _node.Stop().WithTimeout();

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
