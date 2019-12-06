using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class connect_with_link_to_event_with_event_number_greater_than_int_maxvalue
		: IClassFixture<
			connect_with_link_to_event_with_event_number_greater_than_int_maxvalue.Fixture> {
		private readonly Fixture _fixture;

		private const string Stream =
			nameof(connect_with_link_to_event_with_event_number_greater_than_int_maxvalue);

		private const string Group = nameof(Group);

		public connect_with_link_to_event_with_event_number_greater_than_int_maxvalue(
			Fixture fixture) {
			_fixture = fixture;
		}

		[Fact(Skip = "waiting for TestServer to be fixed")]
		public async Task the_subscription_resolves_the_linked_event_correctly() {
			var source = new TaskCompletionSource<ResolvedEvent>();
			using var _ = _fixture.Client.PersistentSubscriptions.Subscribe(
				Stream, Group, EventAppeared, SubscriptionDropped);

			var result = await source.Task.WithTimeout();

			Assert.Equal(new StreamRevision(int.MaxValue + 1L), result.Event.EventNumber);
			Assert.Equal(Fixture.Events[0].EventId, result.Event.EventId);

			Task EventAppeared(PersistentSubscription s, ResolvedEvent e, int? retryCount, CancellationToken ct) {
				source.TrySetResult(e);

				return Task.CompletedTask;
			}

			void SubscriptionDropped(PersistentSubscription subscription, SubscriptionDroppedReason reason,
				Exception ex)
				=> source.TrySetException(ex ?? new ObjectDisposedException(nameof(subscription)));
		}

		public class Fixture : StreamRevisionGreaterThanIntMaxValueFixture {
			public static readonly EventData[] Events = CreateTestEvents(1).ToArray();

			public Fixture() : base((checkpoint, writer) => {
				long revision = int.MaxValue + 1L;
				for (var i = 0; i < Events.Length; i++) {
					var @event = Events[i];
					WriteSingleEvent(
						checkpoint,
						writer,
						Stream,
						revision + i,
						@event.Data,
						eventId: @event.EventId,
						eventType: @event.Type);
				}
			}) {
			}

			protected override string StreamName => Stream;

			protected override Task When() =>
				Client.PersistentSubscriptions.CreateAsync(StreamName, Group,
					new PersistentSubscriptionSettings(startFrom: StreamRevision.Start), TestCredentials.Root);
		}
	}
}
