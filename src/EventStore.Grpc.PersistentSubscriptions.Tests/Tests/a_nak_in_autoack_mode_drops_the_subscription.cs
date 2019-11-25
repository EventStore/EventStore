using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class a_nak_in_autoack_mode_drops_the_subscription
		: IClassFixture<a_nak_in_autoack_mode_drops_the_subscription.Fixture> {
		private readonly Fixture _fixture;
		private const string Group = "naktest";
		private const string Stream = nameof(a_nak_in_autoack_mode_drops_the_subscription);

		public a_nak_in_autoack_mode_drops_the_subscription(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task the_subscription_gets_dropped() {
			var (reason, exception) = await _fixture.SubscriptionDropped.WithTimeout(TimeSpan.FromSeconds(10));
			Assert.Equal(SubscriptionDroppedReason.SubscriberError, reason);
			var ex = Assert.IsType<Exception>(exception);
			Assert.Equal("test", ex.Message);
		}

		public class Fixture : EventStoreGrpcFixture {
			private readonly TaskCompletionSource<(SubscriptionDroppedReason, Exception)> _subscriptionDroppedSource;

			public Task<(SubscriptionDroppedReason reason, Exception exception)> SubscriptionDropped =>
				_subscriptionDroppedSource.Task;

			public readonly EventData[] Events;
			private PersistentSubscription _subscription;

			public Fixture() {
				_subscriptionDroppedSource = new TaskCompletionSource<(SubscriptionDroppedReason, Exception)>();
				Events = CreateTestEvents().ToArray();
			}

			protected override async Task Given() {
				await Client.PersistentSubscriptions.CreateAsync(Stream, Group,
					new PersistentSubscriptionSettings(startFrom: StreamRevision.Start), TestCredentials.Root);
				_subscription = Client.PersistentSubscriptions.Subscribe(Stream, Group,
					delegate {
						throw new Exception("test");
					}, (subscription, reason, ex) => _subscriptionDroppedSource.SetResult((reason, ex)));
			}

			protected override Task When() =>
				Client.AppendToStreamAsync(Stream, AnyStreamRevision.NoStream, Events);

			public override Task DisposeAsync() {
				_subscription?.Dispose();
				return base.DisposeAsync();
			}
		}
	}
}
