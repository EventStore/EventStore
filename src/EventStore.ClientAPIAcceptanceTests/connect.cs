using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Xunit;
using Xunit.Abstractions;

namespace EventStore.ClientAPIAcceptanceTests {
	public class connect : EventStoreClientAPITest, IClassFixture<EventStoreClientAPIFixture> {
		private readonly EventStoreClientAPIFixture _fixture;
		private readonly ITestOutputHelper _testOutputHelper;

		public connect(EventStoreClientAPIFixture fixture, ITestOutputHelper testOutputHelper) {
			_fixture = fixture;
			_testOutputHelper = testOutputHelper;
			_fixture.TestOutputHelper = testOutputHelper;
		}

		public static IEnumerable<object[]> TestCases() {
			yield return new object[] {true};
			yield return new object[] {false};
		}

		[Theory, MemberData(nameof(TestCases))]
		public async Task does_not_throw_when_server_is_down(bool useSsl) {
			using var connection = _fixture.CreateConnection(_fixture.Settings(useSsl),
				EventStoreClientAPIFixture.ExternalPort + 1);
			await connection.ConnectAsync();
		}

		[Theory, MemberData(nameof(TestCases))]
		public async Task reopening_a_closed_connection_throws(bool useSsl) {
			var closedSource = new TaskCompletionSource<bool>();
			using var connection = _fixture.CreateConnection(
				_fixture.Settings(useSsl)
					.LimitReconnectionsTo(0)
					.WithConnectionTimeoutOf(TimeSpan.FromSeconds(10))
					.SetReconnectionDelayTo(TimeSpan.Zero)
					.FailOnNoServerResponse(),
				EventStoreClientAPIFixture.ExternalPort);
			connection.Closed += delegate { closedSource.TrySetResult(true); };
			await connection.ConnectAsync();

			connection.Close();

			await closedSource.Task.WithTimeout(TimeSpan.FromSeconds(120));

			await Assert.ThrowsAsync<ObjectDisposedException>(() => connection.ConnectAsync().WithTimeout());
		}

		[Theory, MemberData(nameof(TestCases))]
		public async Task closes_after_configured_amount_of_failed_reconnections(bool useSsl) {
			var closedSource = new TaskCompletionSource<bool>();
			using var connection = _fixture.CreateConnection(
				_fixture.Settings(useSsl)
					.LimitReconnectionsTo(1)
					.WithConnectionTimeoutOf(TimeSpan.FromSeconds(10))
					.SetReconnectionDelayTo(TimeSpan.Zero)
					.FailOnNoServerResponse(),
				EventStoreClientAPIFixture.ExternalPort + 1);
			connection.Closed += delegate { closedSource.TrySetResult(true); };
			connection.Connected += (s, e) => _testOutputHelper.WriteLine(
				"EventStoreConnection '{0}': connected to [{1}]...", e.Connection.ConnectionName, e.RemoteEndPoint);
			connection.Reconnecting += (s, e) =>
				_testOutputHelper.WriteLine("EventStoreConnection '{0}': reconnecting...", e.Connection.ConnectionName);
			connection.Disconnected += (s, e) =>
				_testOutputHelper.WriteLine("EventStoreConnection '{0}': disconnected from [{1}]...",
					e.Connection.ConnectionName, e.RemoteEndPoint);
			connection.ErrorOccurred += (s, e) => _testOutputHelper.WriteLine("EventStoreConnection '{0}': error = {1}",
				e.Connection.ConnectionName, e.Exception);

			await connection.ConnectAsync();

			await closedSource.Task.WithTimeout(TimeSpan.FromSeconds(120));

			await Assert.ThrowsAsync<ObjectDisposedException>(() => connection.AppendToStreamAsync(
				nameof(closes_after_configured_amount_of_failed_reconnections),
				ExpectedVersion.NoStream,
				_fixture.CreateTestEvents()).WithTimeout());
		}
	}
}
