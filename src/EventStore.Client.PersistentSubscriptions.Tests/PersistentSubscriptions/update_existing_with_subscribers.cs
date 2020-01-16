using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.PersistentSubscriptions {
	public class update_existing_with_subscribers
		: IClassFixture<update_existing_with_subscribers.Fixture> {
		private const string Stream = nameof(update_existing_with_subscribers);
		private const string Group = "existing";
		private readonly Fixture _fixture;

		public update_existing_with_subscribers(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task existing_subscriptions_are_dropped() {
			await _fixture.Started.WithTimeout();
			var (reason, exception) = await _fixture.Dropped.WithTimeout(TimeSpan.FromSeconds(10));
			Assert.Equal(SubscriptionDroppedReason.ServerError, reason);
			var ex = Assert.IsType<PersistentSubscriptionDroppedByServerException>(exception);
			Assert.Equal(Stream, ex.StreamName);
			Assert.Equal(Group, ex.GroupName);
		}

		public class Fixture : ExternalEventStoreGrpcFixture {
			private readonly TaskCompletionSource<(SubscriptionDroppedReason, Exception)> _droppedSource;
			public Task<(SubscriptionDroppedReason, Exception)> Dropped => _droppedSource.Task;
			public Task Started => _subscription.Started;
			private PersistentSubscription _subscription;

			public Fixture() {
				_droppedSource = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();
			}

			protected override async Task Given() {
				await Client.AppendToStreamAsync(Stream, AnyStreamRevision.NoStream, CreateTestEvents());
				await Client.PersistentSubscriptions.CreateAsync(Stream, Group, new PersistentSubscriptionSettings(),
					TestCredentials.Root);
				_subscription = Client.PersistentSubscriptions.Subscribe(Stream, Group,
					delegate { return Task.CompletedTask; },
					(subscription, reason, ex) => _droppedSource.TrySetResult((reason, ex)));
				await _subscription.Started;
			}

			protected override Task When() => Client.PersistentSubscriptions.UpdateAsync(Stream, Group,
				new PersistentSubscriptionSettings(), TestCredentials.Root);

			public override Task DisposeAsync() {
				_subscription?.Dispose();
				return base.DisposeAsync();
			}
		}
	}
}
