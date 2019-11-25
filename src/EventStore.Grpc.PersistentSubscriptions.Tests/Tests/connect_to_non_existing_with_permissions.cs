using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class connect_to_non_existing_with_permissions
		: IClassFixture<connect_to_non_existing_with_permissions.Fixture> {
		private readonly Fixture _fixture;

		public connect_to_non_existing_with_permissions(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task throws_persistent_subscription_not_found() {
			var streamName = _fixture.GetStreamName();
			var dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();
			using var _ = _fixture.Client.PersistentSubscriptions.Subscribe(
				streamName,
				"foo",
				delegate {
					return Task.CompletedTask;
				},
				(s, r, e) => dropped.TrySetResult((r, e)));

			var (reason, exception) = await dropped.Task.WithTimeout();

			Assert.Equal(SubscriptionDroppedReason.ServerError, reason);
			var ex = Assert.IsType<PersistentSubscriptionNotFoundException>(exception);

			Assert.Equal(streamName, ex.StreamName);
			Assert.Equal("foo", ex.GroupName);
		}

		public class Fixture : EventStoreGrpcFixture {
			public Fixture() {
			}

			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
