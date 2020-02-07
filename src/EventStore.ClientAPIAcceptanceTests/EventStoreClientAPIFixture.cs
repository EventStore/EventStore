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
		public IDictionary<SslType, IEventStoreConnection> Connections { get; }

		public EventStoreClientAPIFixture() {
			using var stream = typeof(EventStoreClientAPIFixture)
				.Assembly
				.GetManifestResourceStream(typeof(EventStoreClientAPIFixture), "test_certificates.server.server.p12");
			using var mem = new MemoryStream();
			stream.CopyTo(mem);
			var vNodeBuilder = ClusterVNodeBuilder
				.AsSingleNode()
				.WithExternalTcpOn(new IPEndPoint(IPAddress.Loopback, ExternalPort))
				.WithExternalSecureTcpOn(new IPEndPoint(IPAddress.Loopback, ExternalSecurePort))
				.WithServerCertificate(new X509Certificate2(mem.ToArray(), "password"))
				.RunInMemory()
				.EnableExternalTCP();

			_node = vNodeBuilder.Build();

			Connections = new Dictionary<SslType, IEventStoreConnection>();
			foreach (var sslType in EventStoreClientAPITest.SslTypes) {
				switch (sslType) {
					case SslType.None:
						Connections[sslType] = CreateConnection(settings => settings.UseSsl(sslType), ExternalPort);
						break;
					case SslType.NoClientCertificate:
						Connections[sslType] = CreateConnection(settings => settings.UseSsl(sslType), ExternalSecurePort);
						break;
					case SslType.WithAdminClientCertificate:
						Connections[sslType] = CreateConnection(settings => settings.UseSsl(sslType), ExternalSecurePort);
						break;
				}
			}
		}

		public async Task InitializeAsync() {
			await _node.StartAsync(true);
			foreach (var sslType in EventStoreClientAPITest.SslTypes) {
				await Connections[sslType].ConnectAsync();
			}
		}

		public Task DisposeAsync() {
			foreach (var sslType in EventStoreClientAPITest.SslTypes) {
				Connections[sslType].Dispose();
			}
			return _node.StopAsync().WithTimeout();
		}

		ValueTask IAsyncDisposable.DisposeAsync() => new ValueTask(DisposeAsync());
	}
}
