using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class update_persistent_subscription : EventStoreClientAPITest {
		private const string Group = nameof(update_persistent_subscription);
		private readonly EventStoreClientAPIFixture _fixture;

		public update_persistent_subscription(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task without_credentials_throws(SslType sslType) {
			var streamName = $"{GetStreamName()}_{sslType}";
			var connection = _fixture.Connections[sslType];

			await connection.CreatePersistentSubscriptionAsync(streamName, Group,
				PersistentSubscriptionSettings.Create(), DefaultUserCredentials.Admin).WithTimeout();

			await Assert.ThrowsAsync<AccessDeniedException>(() => connection.UpdatePersistentSubscriptionAsync(
				streamName, Group,
				PersistentSubscriptionSettings.Create(), null)).WithTimeout();
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task with_credentials(SslType sslType) {
			var streamName = $"{GetStreamName()}_{sslType}";
			var connection = _fixture.Connections[sslType];

			await connection.CreatePersistentSubscriptionAsync(streamName, Group,
				PersistentSubscriptionSettings.Create(), DefaultUserCredentials.Admin).WithTimeout();

			await connection.UpdatePersistentSubscriptionAsync(streamName, Group,
				PersistentSubscriptionSettings.Create(), DefaultUserCredentials.Admin).WithTimeout();
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task when_they_do_not_exist_throws(SslType sslType) {
			var streamName = $"{GetStreamName()}_{sslType}";
			var connection = _fixture.Connections[sslType];

			await Assert.ThrowsAsync<InvalidOperationException>(() => connection.UpdatePersistentSubscriptionAsync(
					streamName, Group, PersistentSubscriptionSettings.Create(), DefaultUserCredentials.Admin))
				.WithTimeout();
		}
	}
}
