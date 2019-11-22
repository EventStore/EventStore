using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class create_persistent_subscription_on_all_stream
		: IClassFixture<create_persistent_subscription_on_all_stream.Fixture> {
		public create_persistent_subscription_on_all_stream(Fixture fixture) {
			_fixture = fixture;
		}

		private readonly Fixture _fixture;

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}

		[Fact]
		public Task the_completion_fails_with_invalid_stream() =>
			Assert.ThrowsAsync<InvalidOperationException>(() =>
				_fixture.Client.PersistentSubscriptions.CreateAsync("$all", "shitbird",
					new PersistentSubscriptionSettings(), TestCredentials.Root));
	}
}
