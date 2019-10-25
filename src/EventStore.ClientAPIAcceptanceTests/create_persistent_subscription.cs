using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using Xunit;

namespace EventStore.ClientAPIAcceptanceTests {
	[Collection(nameof(EventStoreClientAPIFixture))]
	public class create_persistent_subscription : EventStoreClientAPITest, IClassFixture<EventStoreClientAPIFixture> {
		private const string Group = nameof(create_persistent_subscription);
		private readonly EventStoreClientAPIFixture _fixture;

		public create_persistent_subscription(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task without_credentials_throws(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync();

			await Assert.ThrowsAsync<AccessDeniedException>(() => connection.CreatePersistentSubscriptionAsync(
				streamName, Group,
				PersistentSubscriptionSettings.Create(), null));
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task with_credentials(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync();

			await connection.CreatePersistentSubscriptionAsync(streamName, Group,
				PersistentSubscriptionSettings.Create(), DefaultUserCredentials.Admin);
		}
	}
}
