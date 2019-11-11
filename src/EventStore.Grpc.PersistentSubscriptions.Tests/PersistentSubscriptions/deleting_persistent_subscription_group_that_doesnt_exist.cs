using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class deleting_persistent_subscription_group_that_doesnt_exist
		: IClassFixture<deleting_persistent_subscription_group_that_doesnt_exist.Fixture> {
		private readonly Fixture _fixture;
		private const string Stream = nameof(deleting_persistent_subscription_group_that_doesnt_exist);

		public deleting_persistent_subscription_group_that_doesnt_exist(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task the_delete_fails_with_argument_exception() {
			await Assert.ThrowsAsync<PersistentSubscriptionNotFoundException>(
				() => _fixture.Client.PersistentSubscriptions.DeleteAsync(Stream,
					Guid.NewGuid().ToString(), TestCredentials.Root));
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
