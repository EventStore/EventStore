using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Services;
using EventStore.Core.TransactionLog.Services;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class read_all_backward : EventStoreClientAPITest,
		IAsyncLifetime {
		private readonly EventStoreClientAPIFixture _fixture;

		public read_all_backward(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task returns_expected_result(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			var connection = _fixture.Connections[useSsl];

			var testEvents = _fixture.CreateTestEvents(3).ToArray();

			var writeResult = await connection.AppendToStreamAsync(streamName, ExpectedVersion.NoStream, testEvents)
				.WithTimeout();

			var result = await connection.ReadAllEventsBackwardAsync(Position.End, 4096, false).WithTimeout();

			Assert.Equal(ReadDirection.Backward, result.ReadDirection);
			Assert.True(result.Events.Length >= testEvents.Length);
			Assert.Equal(testEvents.Select(x => x.EventId), result.Events
				.Reverse()
				.Where(x => x.OriginalStreamId == streamName)
				.Select(x => x.OriginalEvent.EventId));
		}

		public async Task InitializeAsync() {
			var connection = _fixture.Connections[false];;

			await connection.SetStreamMetadataAsync("$all", ExpectedVersion.Any,
				StreamMetadata.Build().SetReadRole(SystemRoles.All), DefaultUserCredentials.Admin).WithTimeout();
		}

		public async Task DisposeAsync() {
			var connection = _fixture.Connections[false];;

			await connection.SetStreamMetadataAsync("$all", ExpectedVersion.Any,
				StreamMetadata.Build().SetReadRole(null), DefaultUserCredentials.Admin).WithTimeout();
		}
	}
}
