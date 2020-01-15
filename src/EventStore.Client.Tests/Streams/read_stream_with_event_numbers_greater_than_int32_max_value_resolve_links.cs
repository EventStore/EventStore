using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Convert = System.Convert;

namespace EventStore.Client.Streams {
	public class read_stream_with_event_numbers_greater_than_int32_max_value_resolve_links
		: IClassFixture<read_stream_with_event_numbers_greater_than_int32_max_value_resolve_links.Fixture> {
		private const string Stream = nameof(read_stream_with_event_numbers_greater_than_int32_max_value);
		private const string LinkedStream = "linked-" + Stream;

		private readonly Fixture _fixture;

		public read_stream_with_event_numbers_greater_than_int32_max_value_resolve_links(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task should_be_able_to_read_linked_stream_forward() {
			var result = await _fixture.Client
				.ReadStreamForwardsAsync(LinkedStream, StreamRevision.Start, 100, true)
				.ToArrayAsync();

			Assert.Equal(2, result.Length);
			Assert.Equal(Fixture.Events.Select(x => x.EventId), result.Select(x => x.Event.EventId));
			Assert.Equal(Enumerable.Range(1, 2)
				.Select(Convert.ToUInt64)
				.Select(x => new StreamRevision(int.MaxValue + x)), result.Select(x => x.Event.EventNumber));
		}

		[Fact]
		public async Task should_be_able_to_read_stream_backward() {
			var result = await _fixture.Client
				.ReadStreamBackwardsAsync(LinkedStream, new StreamRevision(10), 100, true)
				.Reverse()
				.ToArrayAsync();

			Assert.Equal(2, result.Length);
			Assert.Equal(Fixture.Events.Select(x => x.EventId), result.Select(x => x.Event.EventId));
			Assert.Equal(Enumerable.Range(1, 2)
				.Select(Convert.ToUInt64)
				.Select(x => new StreamRevision(int.MaxValue + x)), result.Select(x => x.Event.EventNumber));
		}


		[Fact]
		public async Task should_be_able_to_read_all_forward() {
			var result = await _fixture.Client.ReadAllForwardsAsync(Position.Start, 100, true, userCredentials: TestCredentials.Root)
				.Where(x => x.OriginalStreamId == LinkedStream)
				.ToArrayAsync();

			Assert.Equal(2, result.Length);
			Assert.Equal(Fixture.Events.Select(x => x.EventId), result.Select(x => x.Event.EventId));
			Assert.Equal(Enumerable.Range(1, 2)
				.Select(Convert.ToUInt64)
				.Select(x => new StreamRevision(int.MaxValue + x)), result.Select(x => x.Event.EventNumber));
		}

		[Fact]
		public async Task should_be_able_to_read_all_backward() {
			var result = await _fixture.Client.ReadAllBackwardsAsync(Position.End, 100, true, userCredentials: TestCredentials.Root)
				.Where(x => x.OriginalStreamId == LinkedStream)
				.Reverse()
				.ToArrayAsync();

			Assert.Equal(2, result.Length);
			Assert.Equal(Fixture.Events.Select(x => x.EventId), result.Select(x => x.Event.EventId));
			Assert.Equal(Enumerable.Range(1, 2)
				.Select(Convert.ToUInt64)
				.Select(x => new StreamRevision(int.MaxValue + x)), result.Select(x => x.Event.EventNumber));
		}

		public class Fixture : StreamRevisionGreaterThanIntMaxValueFixture {
			protected override string StreamName => Stream;

			protected override Task When() => Task.CompletedTask;

			public static readonly EventData[] Events = CreateTestEvents(2).ToArray();

			public static readonly EventData[] LinkedEvents = {
				CreateLinkToEvent(Stream, new StreamRevision(int.MaxValue + 1L)),
				CreateLinkToEvent(Stream, new StreamRevision(int.MaxValue + 2L))
			};

			public Fixture() : base((checkpoint, writer) => {
				long revision = int.MaxValue + 1L;
				for (var i = 0; i < Events.Length; i++) {
					var @event = Events[i];
					var link = LinkedEvents[i];
					WriteSingleEvent(
						checkpoint,
						writer,
						Stream,
						revision + i,
						@event.Data,
						eventId: @event.EventId,
						eventType: @event.Type);

					WriteSingleEvent(
						checkpoint,
						writer,
						LinkedStream,
						i,
						link.Data,
						eventId: link.EventId,
						eventType: link.Type);
				}
			}) {
			}
		}
	}
}
