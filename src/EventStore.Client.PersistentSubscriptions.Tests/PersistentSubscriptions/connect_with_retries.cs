using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.PersistentSubscriptions {
	public class connect_with_retries
		: IClassFixture<connect_with_retries.Fixture> {
		private readonly Fixture _fixture;
		private const string Group = "retries";

		private const string Stream = nameof(connect_with_retries);

		public connect_with_retries(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task events_are_retried_until_success() {
			Assert.Equal(5, await _fixture.RetryCount.WithTimeout());
		}

		public class Fixture : EventStoreGrpcFixture {
			private readonly TaskCompletionSource<int> _retryCountSource;
			public Task<int> RetryCount => _retryCountSource.Task;
			public readonly EventData[] Events;
			private PersistentSubscription _subscription;

			public Fixture() {
				_retryCountSource = new TaskCompletionSource<int>();
				Events = CreateTestEvents().ToArray();
			}

			protected override async Task Given() {
				await Client.AppendToStreamAsync(Stream, AnyStreamRevision.NoStream, Events);
				await Client.PersistentSubscriptions.CreateAsync(Stream, Group,
					new PersistentSubscriptionSettings(startFrom: StreamRevision.Start), TestCredentials.Root);
				_subscription = Client.PersistentSubscriptions.Subscribe(Stream, Group,
					async (subscription, e, r, ct) => {
						if (r > 4) {
							_retryCountSource.TrySetResult(r.Value);
							await subscription.Ack(e.Event.EventId);
						} else {
							await subscription.Nack(PersistentSubscriptionNakEventAction.Retry,
								"Not yet tried enough times", e);
						}
					}, autoAck: false);
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
