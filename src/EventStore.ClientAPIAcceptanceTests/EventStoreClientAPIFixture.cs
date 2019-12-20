using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using EventStore.ClusterNode;
using EventStore.Core;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public partial class EventStoreClientAPIFixture : IAsyncLifetime, IAsyncDisposable {
		private readonly ClusterVNode _node;
		public IDictionary<bool, IEventStoreConnection> Connections { get; }

		public EventStoreClientAPIFixture() {
			using var stream = typeof(EventStoreClientAPIFixture)
				.Assembly
				.GetManifestResourceStream(typeof(EventStoreClientAPIFixture), "server.p12");
			using var mem = new MemoryStream();
			stream.CopyTo(mem);
			var vNodeBuilder = ClusterVNodeBuilder
				.AsSingleNode()
				.WithExternalTcpOn(new IPEndPoint(IPAddress.Loopback, ExternalPort))
				.WithExternalSecureTcpOn(new IPEndPoint(IPAddress.Loopback, ExternalSecurePort))
				.WithServerCertificate(new X509Certificate2(mem.ToArray(), "1111"))
				.RunInMemory();

			_node = vNodeBuilder.Build();
			Connections = new Dictionary<bool, IEventStoreConnection> {
				[false] = CreateConnection(settings => settings.UseSsl(false), ExternalPort),
				[true] = CreateConnection(settings => settings.UseSsl(true), ExternalSecurePort)
			};
		}

		public async Task InitializeAsync() {
			await _node.StartAsync(true);
			await Connections[true].ConnectAsync();
			await Connections[false].ConnectAsync();
		}

		public Task DisposeAsync() {
			Connections[true].Dispose();
			Connections[false].Dispose();
			return _node.Stop().WithTimeout();
		}

		ValueTask IAsyncDisposable.DisposeAsync() => new ValueTask(DisposeAsync());
	}
}
