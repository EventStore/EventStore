using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	public class subscribe_to_all_with_event_numbers_greater_than_int32_max_value
		: IClassFixture<subscribe_to_all_with_event_numbers_greater_than_int32_max_value.Fixture> {
		private const string Stream = nameof(subscribe_to_all_with_event_numbers_greater_than_int32_max_value);
		private readonly Fixture _fixture;

		public subscribe_to_all_with_event_numbers_greater_than_int32_max_value(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task should_subscribe() {
			var source = new TaskCompletionSource<bool>();
			var @event = new EventData(Uuid.NewUuid(), "-", Array.Empty<byte>(),
				contentType: Constants.Metadata.ContentTypes.ApplicationOctetStream);
			var received = new List<Uuid>(3);
			using var _ = await _fixture.Client.SubscribeToAllAsync(
				Position.Start, EventAppeared, false, SubscriptionDropped, userCredentials: TestCredentials.Root);

			await _fixture.Client.AppendToStreamAsync(Stream, AnyStreamRevision.Any, new[] {@event});

			await source.Task.WithTimeout();

			Assert.Equal(
				Fixture.Events.Select(x => x.EventId).Concat(new[] {@event.EventId}),
				received);

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) {
				if (e.OriginalStreamId == Stream) {
					received.Add(e.Event.EventId);
					if (received.Count >= 3) {
						source.TrySetResult(true);
					}
				}

				return Task.CompletedTask;
			}

			void SubscriptionDropped(StreamSubscription subscription, SubscriptionDroppedReason reason, Exception ex)
				=> source.TrySetException(ex ?? new ObjectDisposedException(nameof(subscription)));
		}

		public class Fixture : StreamRevisionGreaterThanIntMaxValueFixture {
			public static readonly EventData[] Events = CreateTestEvents(2).ToArray();

			public Fixture() : base((checkpoint, writer) => {
				long revision = int.MaxValue + 1L;
				for (var i = 0; i < Events.Length; i++) {
					var @event = Events[i];
					WriteSingleEvent(
						checkpoint,
						writer,
						Stream,
						revision + i + 1L,
						Array.Empty<byte>(),
						eventId: @event.EventId,
						eventType: @event.Type);
				}
			}) {
			}

			protected override Task When() => Task.CompletedTask;

			protected override string StreamName => Stream;
		}
	}
}
