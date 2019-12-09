using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class connect_to_existing_with_start_from_beginning_and_events_in_it
		: IClassFixture<connect_to_existing_with_start_from_beginning_and_events_in_it.Fixture
		> {
		private readonly Fixture _fixture;

		private const string Group = "startinbeginning1";

		private const string Stream =
			nameof(connect_to_existing_with_start_from_beginning_and_events_in_it);

		public connect_to_existing_with_start_from_beginning_and_events_in_it(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task the_subscription_gets_event_zero_as_its_first_event() {
			var resolvedEvent = await _fixture.FirstEvent.WithTimeout(TimeSpan.FromSeconds(10));
			Assert.Equal(StreamRevision.Start, resolvedEvent.Event.EventNumber);
			Assert.Equal(_fixture.Events[0].EventId, resolvedEvent.Event.EventId);
		}

		public class Fixture : EventStoreGrpcFixture {
			private readonly TaskCompletionSource<ResolvedEvent> _firstEventSource;
			public Task<ResolvedEvent> FirstEvent => _firstEventSource.Task;
			public readonly EventData[] Events;
			private PersistentSubscription _subscription;

			public Fixture() {
				_firstEventSource = new TaskCompletionSource<ResolvedEvent>();
				Events = CreateTestEvents(10).ToArray();
			}

			protected override async Task Given() {
				await Client.AppendToStreamAsync(Stream, AnyStreamRevision.NoStream, Events);
				await Client.PersistentSubscriptions.CreateAsync(Stream, Group,
					new PersistentSubscriptionSettings(startFrom: StreamRevision.Start), TestCredentials.Root);
			}

			protected override Task When() {
				_subscription = Client.PersistentSubscriptions.Subscribe(Stream, Group,
					(subscription, e, r, ct) => {
						_firstEventSource.TrySetResult(e);
						return Task.CompletedTask;
					});
				return _subscription.Started;
			}

			public override Task DisposeAsync() {
				_subscription?.Dispose();
				return base.DisposeAsync();
			}
		}
	}
}
