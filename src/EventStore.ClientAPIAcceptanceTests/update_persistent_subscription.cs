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
		public async Task without_credentials_throws(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync().WithTimeout();

			await connection.CreatePersistentSubscriptionAsync(streamName, Group,
				PersistentSubscriptionSettings.Create(), DefaultUserCredentials.Admin).WithTimeout();

			await Assert.ThrowsAsync<AccessDeniedException>(() => connection.UpdatePersistentSubscriptionAsync(
				streamName, Group,
				PersistentSubscriptionSettings.Create(), null)).WithTimeout();
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task with_credentials(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync().WithTimeout();

			await connection.CreatePersistentSubscriptionAsync(streamName, Group,
				PersistentSubscriptionSettings.Create(), DefaultUserCredentials.Admin).WithTimeout();

			await connection.UpdatePersistentSubscriptionAsync(streamName, Group,
				PersistentSubscriptionSettings.Create(), DefaultUserCredentials.Admin).WithTimeout();
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task when_they_do_not_exist_throws(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync().WithTimeout();

			await Assert.ThrowsAsync<InvalidOperationException>(() => connection.UpdatePersistentSubscriptionAsync(
					streamName, Group, PersistentSubscriptionSettings.Create(), DefaultUserCredentials.Admin))
				.WithTimeout();
		}
	}
}
