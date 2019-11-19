using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.ClientAPI.Tests {
	internal class EventStoreDockerContainer : IAsyncDisposable {
		private readonly int _port;
		private readonly DockerContainer _container;

		public EventStoreDockerContainer(int port, int securePort) {
			_port = port;
			_container = new DockerContainer("eventstore-ci-test", "latest", HealthCheck, new Dictionary<int, int> {
				[1113] = port,
				[1114] = securePort
			}) {
				Env = new[] {
					"EVENTSTORE_EXT_TCP_PORT=1113",
					"EVENTSTORE_EXT_SECURE_TCP_PORT=1114"
				},
				ContainerName = "eventstore-ci-test"
			};
		}

		private async Task<bool> HealthCheck(CancellationToken cancellationToken) {
			using var connection =
				EventStoreConnection.Create(ConnectionSettings.Create(), new IPEndPoint(IPAddress.Loopback, _port));
			try {
				await connection.ConnectAsync();
				return true;
			} catch {
				return false;
			}
		}

		public async ValueTask TryStart(CancellationToken cancellationToken = default) {
			await _container.TryStart(cancellationToken);
		}

		public ValueTask DisposeAsync() => _container.DisposeAsync();
	}
}
