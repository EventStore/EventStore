using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class update_non_existing_persistent_subscription
		: IClassFixture<update_non_existing_persistent_subscription.Fixture> {
		private const string Stream = nameof(update_non_existing_persistent_subscription);
		private const string Group = "existing";
		private readonly Fixture _fixture;

		public update_non_existing_persistent_subscription(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task the_completion_fails_with_not_found() {
			await Assert.ThrowsAsync<InvalidOperationException>(
				() => _fixture.Client.PersistentSubscriptions.UpdateAsync(Stream, Group,
					new PersistentSubscriptionSettings(), TestCredentials.Root));
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
