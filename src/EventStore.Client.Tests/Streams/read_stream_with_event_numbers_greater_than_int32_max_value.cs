using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	public class read_stream_with_event_numbers_greater_than_int32_max_value
		: IClassFixture<read_stream_with_event_numbers_greater_than_int32_max_value.Fixture> {
		private const string Stream = nameof(read_stream_with_event_numbers_greater_than_int32_max_value);

		private readonly Fixture _fixture;

		public read_stream_with_event_numbers_greater_than_int32_max_value(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task read_forward_from_zero() {
			var result = await _fixture.Client.ReadStreamForwardsAsync(Stream, StreamRevision.Start, 100)
				.ToArrayAsync();
			Assert.Empty(result);
		}

		[Fact]
		public async Task should_be_able_to_read_stream_forward() {
			var result = await _fixture.Client.ReadStreamForwardsAsync(Stream, new StreamRevision(int.MaxValue), 100)
				.ToArrayAsync();
			Assert.Equal(5, result.Length);
			Assert.Equal(Fixture.Events.Select(x => x.EventId), result.Select(x => x.Event.EventId));
		}

		[Fact]
		public async Task should_be_able_to_read_stream_backward() {
			var result =
				await _fixture.Client.ReadStreamBackwardsAsync(Stream, new StreamRevision(int.MaxValue + 6L), 100)
					.Reverse()
					.ToArrayAsync();
			Assert.Equal(5, result.Length);
			Assert.Equal(Fixture.Events.Select(x => x.EventId), result.Select(x => x.Event.EventId));
		}


		[Fact]
		public async Task should_be_able_to_read_all_forward() {
			var result = await _fixture.Client.ReadAllForwardsAsync(Position.Start, 100, false, userCredentials: TestCredentials.Root)
				.Where(x => x.OriginalStreamId == Stream)
				.ToArrayAsync();
			Assert.Equal(5, result.Length);
			Assert.Equal(Fixture.Events.Select(x => x.EventId), result.Select(x => x.Event.EventId));
		}

		[Fact]
		public async Task should_be_able_to_read_all_backward() {
			var result = await _fixture.Client.ReadAllBackwardsAsync(Position.End, 100, false, userCredentials: TestCredentials.Root)
				.Where(x => x.OriginalStreamId == Stream)
				.Reverse()
				.ToArrayAsync();
			Assert.Equal(5, result.Length);
			Assert.Equal(Fixture.Events.Select(x => x.EventId), result.Select(x => x.Event.EventId));
		}

		public class Fixture : StreamRevisionGreaterThanIntMaxValueFixture {
			protected override string StreamName => Stream;

			protected override Task When() => Task.CompletedTask;

			public static readonly EventData[] Events = CreateTestEvents(5).ToArray();

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
		}
	}
}
