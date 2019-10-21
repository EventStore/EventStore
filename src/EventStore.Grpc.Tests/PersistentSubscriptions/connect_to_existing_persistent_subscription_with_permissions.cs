using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.Tests.PersistentSubscriptions {
	[Collection(nameof(ExternalKestrelPipelineCollection))]
	public class connect_to_existing_persistent_subscription_with_permissions
		: IClassFixture<connect_to_existing_persistent_subscription_with_permissions.Fixture> {
		private const string Stream = nameof(connect_to_existing_persistent_subscription_with_permissions);

		private readonly Fixture _fixture;

		public connect_to_existing_persistent_subscription_with_permissions(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task the_subscription_succeeds() {
			using var subscription = _fixture.Client.PersistentSubscriptions.Subscribe(Stream,
				"agroupname17",
				delegate { return Task.CompletedTask; }, userCredentials: TestCredentials.Root);
			Assert.NotNull(subscription);
		}

		public class Fixture : EventStoreGrpcFixture {
			public Fixture() : base(external: true) {
			}

			protected override Task Given() =>
				Client.PersistentSubscriptions.CreateAsync(
					Stream,
					"agroupname17",
					new PersistentSubscriptionSettings(),
					TestCredentials.Root);

			protected override Task When() => Task.CompletedTask;
		}
	}
}
