using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.Streams {
	public class subscribe_to_stream_with_event_numbers_greater_than_int32_max_value
		: IClassFixture<subscribe_to_stream_with_event_numbers_greater_than_int32_max_value.Fixture> {
		private const string Stream = nameof(subscribe_to_stream_with_event_numbers_greater_than_int32_max_value);

		private const string LinkedStream = "linked-" + Stream;
		private readonly Fixture _fixture;

		public subscribe_to_stream_with_event_numbers_greater_than_int32_max_value(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task should_subscribe() {
			var source = new TaskCompletionSource<ResolvedEvent>();
			using var _ = _fixture.Client.SubscribeToStream(
				Stream, StreamRevision.Start, EventAppeared, false, SubscriptionDropped);

			var result = await source.Task.WithTimeout();

			Assert.Equal(Fixture.Events[0].EventId, result.Event.EventId);

			Task EventAppeared(StreamSubscription s, ResolvedEvent e, CancellationToken ct) {
				source.TrySetResult(e);

				return Task.CompletedTask;
			}

			void SubscriptionDropped(StreamSubscription subscription, SubscriptionDroppedReason reason, Exception ex)
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
						Array.Empty<byte>(),
						eventId: @event.EventId,
						eventType: @event.Type);
				}
			}) {
			}

			protected override Task When() => Client.AppendToStreamAsync(LinkedStream, AnyStreamRevision.NoStream,
				new[] {CreateLinkToEvent(Stream, new StreamRevision(int.MaxValue + 1L))});

			protected override string StreamName => Stream;
		}
	}
}
