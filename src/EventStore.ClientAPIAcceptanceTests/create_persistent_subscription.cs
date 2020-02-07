using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class create_persistent_subscription : EventStoreClientAPITest {
		private const string Group = nameof(create_persistent_subscription);
		private readonly EventStoreClientAPIFixture _fixture;

		public create_persistent_subscription(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task without_credentials_throws(SslType sslType) {
			var streamName = $"{GetStreamName()}_{sslType}";
			var connection = _fixture.Connections[sslType];

			await Assert.ThrowsAsync<AccessDeniedException>(() => connection.CreatePersistentSubscriptionAsync(
				streamName, Group,
				PersistentSubscriptionSettings.Create(), null).WithTimeout());
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task with_credentials(SslType sslType) {
			var streamName = $"{GetStreamName()}_{sslType}";
			var connection = _fixture.Connections[sslType];

			await connection.CreatePersistentSubscriptionAsync(streamName, Group,
				PersistentSubscriptionSettings.Create(), DefaultUserCredentials.Admin).WithTimeout();
		}
	}
}
