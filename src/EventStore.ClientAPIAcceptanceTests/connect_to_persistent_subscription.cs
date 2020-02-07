using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class connect_to_persistent_subscription
		: EventStoreClientAPITest {
		private const string Group = nameof(connect_to_persistent_subscription);
		private readonly EventStoreClientAPIFixture _fixture;

		public connect_to_persistent_subscription(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory(Skip = "busted on 5.x"), MemberData(nameof(UseSslTestCases))]
		public async Task that_does_not_exist_throws(SslType sslType) {
			var streamName = $"{GetStreamName()}_{sslType}";
			var connection = _fixture.Connections[sslType];

			await Assert.ThrowsAsync<ArgumentException>(() => connection.ConnectToPersistentSubscriptionAsync(
				streamName, Group,
				delegate { return Task.CompletedTask; })).WithTimeout();
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task that_does_exist_succeeds(SslType sslType) {
			var streamName = $"{GetStreamName()}_{sslType}";
			var connection = _fixture.Connections[sslType];

			await connection.CreatePersistentSubscriptionAsync(streamName, Group,
				PersistentSubscriptionSettings.Create(), DefaultUserCredentials.Admin).WithTimeout();

			await connection.ConnectToPersistentSubscriptionAsync(streamName, Group,
				delegate { return Task.CompletedTask; }).WithTimeout();
		}
	}
}
