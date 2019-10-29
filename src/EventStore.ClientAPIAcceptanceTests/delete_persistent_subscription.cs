using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class delete_persistent_subscription : EventStoreClientAPITest, IClassFixture<EventStoreClientAPIFixture> {
		private const string Group = nameof(delete_persistent_subscription);
		private readonly EventStoreClientAPIFixture _fixture;

		public delete_persistent_subscription(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task without_credentials_fails(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync();

			await connection.CreatePersistentSubscriptionAsync(streamName, Group,
				PersistentSubscriptionSettings.Create(), DefaultUserCredentials.Admin);

			await Assert.ThrowsAsync<AccessDeniedException>(
				() => connection.DeletePersistentSubscriptionAsync(streamName, Group));
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task that_does_not_exist_fails(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync();

			await Assert.ThrowsAsync<InvalidOperationException>(
				() => connection.DeletePersistentSubscriptionAsync(streamName, Group, DefaultUserCredentials.Admin));
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task with_credentials_succeeds(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync();

			await connection.CreatePersistentSubscriptionAsync(streamName, Group,
				PersistentSubscriptionSettings.Create(), DefaultUserCredentials.Admin);

			await connection.DeletePersistentSubscriptionAsync(streamName, Group,
				DefaultUserCredentials.Admin);
		}
	}
}
