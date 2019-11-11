using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class can_create_duplicate_persistent_subscription_group_name_on_different_streams
		: IClassFixture<can_create_duplicate_persistent_subscription_group_name_on_different_streams.Fixture> {
		public can_create_duplicate_persistent_subscription_group_name_on_different_streams(Fixture fixture) {
			_fixture = fixture;
		}

		private const string Stream =
			nameof(can_create_duplicate_persistent_subscription_group_name_on_different_streams);

		private readonly Fixture _fixture;

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;

			protected override Task When() =>
				Client.PersistentSubscriptions.CreateAsync(Stream, "group3211",
					new PersistentSubscriptionSettings(), TestCredentials.Root);
		}

		[Fact]
		public Task the_completion_succeeds() =>
			_fixture.Client.PersistentSubscriptions.CreateAsync("someother" + Stream,
				"group3211", new PersistentSubscriptionSettings(), TestCredentials.Root);
	}
}
