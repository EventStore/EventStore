using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class create_duplicate_persistent_subscription_group
		: IClassFixture<create_duplicate_persistent_subscription_group.Fixture> {
		public create_duplicate_persistent_subscription_group(Fixture fixture) {
			_fixture = fixture;
		}

		private const string Stream = nameof(create_duplicate_persistent_subscription_group);
		private readonly Fixture _fixture;

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;

			protected override Task When() =>
				Client.PersistentSubscriptions.CreateAsync(Stream, "group32",
					new PersistentSubscriptionSettings(), TestCredentials.Root);
		}

		[Fact]
		public Task the_completion_fails_with_invalid_operation_exception() =>
			Assert.ThrowsAsync<InvalidOperationException>(
				() => _fixture.Client.PersistentSubscriptions.CreateAsync(Stream, "group32",
					new PersistentSubscriptionSettings(),
					TestCredentials.Root));
	}
}
