using System;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Xunit;
using Xunit.Abstractions;

namespace EventStore.ClientAPI.Tests {
	public class connect : EventStoreClientAPITest {
		private readonly EventStoreClientAPIFixture _fixture;

		public connect(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}


		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task does_not_throw_when_server_is_down(SslType sslType) {
			using var connection = _fixture.CreateConnection(builder => builder.UseSsl(sslType),
				EventStoreClientAPIFixture.ExternalPort + 1);
			await connection.ConnectAsync().WithTimeout();
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task reopening_a_closed_connection_throws(SslType sslType) {
			var closedSource = new TaskCompletionSource<bool>();
			using var connection = _fixture.CreateConnection(builder => builder
					.UseSsl(sslType)
					.LimitReconnectionsTo(0)
					.WithConnectionTimeoutOf(TimeSpan.FromSeconds(10))
					.SetReconnectionDelayTo(TimeSpan.Zero)
					.FailOnNoServerResponse(),
				EventStoreClientAPIFixture.ExternalPort);
			connection.Closed += delegate { closedSource.TrySetResult(true); };
			await connection.ConnectAsync().WithTimeout();

			connection.Close();

			await closedSource.Task.WithTimeout(TimeSpan.FromSeconds(120));

			await Assert.ThrowsAsync<ObjectDisposedException>(() => connection.ConnectAsync().WithTimeout());
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task closes_after_configured_amount_of_failed_reconnections(SslType sslType) {
			var closedSource = new TaskCompletionSource<bool>();
			using var connection = _fixture.CreateConnection(
				builder => builder.UseSsl(sslType)
					.LimitReconnectionsTo(1)
					.WithConnectionTimeoutOf(TimeSpan.FromSeconds(10))
					.SetReconnectionDelayTo(TimeSpan.Zero)
					.FailOnNoServerResponse(),
				EventStoreClientAPIFixture.UnusedPort);
			connection.Closed += delegate { closedSource.TrySetResult(true); };
			connection.Connected += (s, e) => Console.WriteLine(
				"EventStoreConnection '{0}': connected to [{1}]...", e.Connection.ConnectionName, e.RemoteEndPoint);
			connection.Reconnecting += (s, e) =>
				Console.WriteLine("EventStoreConnection '{0}': reconnecting...", e.Connection.ConnectionName);
			connection.Disconnected += (s, e) =>
				Console.WriteLine("EventStoreConnection '{0}': disconnected from [{1}]...",
					e.Connection.ConnectionName, e.RemoteEndPoint);
			connection.ErrorOccurred += (s, e) => Console.WriteLine("EventStoreConnection '{0}': error = {1}",
				e.Connection.ConnectionName, e.Exception);

			await connection.ConnectAsync().WithTimeout();

			await closedSource.Task.WithTimeout(TimeSpan.FromSeconds(120));

			await Assert.ThrowsAsync<ObjectDisposedException>(() => connection.AppendToStreamAsync(
				nameof(closes_after_configured_amount_of_failed_reconnections),
				ExpectedVersion.NoStream,
				_fixture.CreateTestEvents()).WithTimeout());
		}
	}
}
