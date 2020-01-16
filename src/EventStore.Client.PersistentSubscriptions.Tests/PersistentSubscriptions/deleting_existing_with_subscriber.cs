using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.PersistentSubscriptions {
	public class deleting_existing_with_subscriber
		: IClassFixture<deleting_existing_with_subscriber.Fixture> {
		private readonly Fixture _fixture;
		private const string Stream = nameof(deleting_existing_with_subscriber);

		public deleting_existing_with_subscriber(Fixture fixture) {
			_fixture = fixture;
		}

		public class Fixture : ExternalEventStoreGrpcFixture {
			public Task<(SubscriptionDroppedReason reason, Exception exception)> Dropped => _dropped.Task;
			private readonly TaskCompletionSource<(SubscriptionDroppedReason, Exception)> _dropped;
			private PersistentSubscription _subscription;

			public Fixture() {
				_dropped = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();
			}

			protected override async Task Given() {
				await Client.PersistentSubscriptions.CreateAsync(Stream, "groupname123",
					new PersistentSubscriptionSettings(),
					TestCredentials.Root);
				_subscription = Client.PersistentSubscriptions.Subscribe(Stream, "groupname123",
					(s, e, i, ct) => Task.CompletedTask,
					(s, r, e) => _dropped.TrySetResult((r, e)));
				await _subscription.Started;
			}

			protected override Task When() =>
				Client.PersistentSubscriptions.DeleteAsync(Stream, "groupname123",
					TestCredentials.Root);

			public override Task DisposeAsync() {
				_subscription?.Dispose();
				return base.DisposeAsync();
			}
		}

		[Fact]
		public async Task the_subscription_is_dropped() {
			var (reason, exception) = await _fixture.Dropped.WithTimeout();
			Assert.Equal(SubscriptionDroppedReason.ServerError, reason);
			var ex = Assert.IsType<PersistentSubscriptionDroppedByServerException>(exception);
			Assert.Equal(Stream, ex.StreamName);
			Assert.Equal("groupname123", ex.GroupName);
		}

		[Fact (Skip = "Isn't this how it should work?")]
		public async Task the_subscription_is_dropped_with_not_found() {
			var (reason, exception) = await _fixture.Dropped.WithTimeout();
			Assert.Equal(SubscriptionDroppedReason.ServerError, reason);
			var ex = Assert.IsType<PersistentSubscriptionNotFoundException>(exception);
			Assert.Equal(Stream, ex.StreamName);
			Assert.Equal("groupname123", ex.GroupName);
		}
	}
}
