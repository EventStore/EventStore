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

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task that_does_not_exist_throws(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync().WithTimeout();

			await Assert.ThrowsAsync<ArgumentException>(() => connection.ConnectToPersistentSubscriptionAsync(
				streamName, Group,
				delegate { return Task.CompletedTask; })).WithTimeout();
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task that_does_exist_succeeds(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync().WithTimeout();

			await connection.CreatePersistentSubscriptionAsync(streamName, Group,
				PersistentSubscriptionSettings.Create(), DefaultUserCredentials.Admin).WithTimeout();

			await connection.ConnectToPersistentSubscriptionAsync(streamName, Group,
				delegate { return Task.CompletedTask; }).WithTimeout();
		}
	}
}
