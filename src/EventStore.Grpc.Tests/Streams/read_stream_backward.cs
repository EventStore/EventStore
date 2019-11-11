using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.Streams {
	[Trait("Category", "Network")]
	public class read_stream_backward : IClassFixture<read_stream_backward.Fixture> {
		private readonly Fixture _fixture;

		public read_stream_backward(Fixture fixture) {
			_fixture = fixture;
		}

		[Theory, InlineData(0), InlineData(-1), InlineData(int.MinValue)]
		public async Task count_le_equal_zero_throws(int count) {
			var stream = _fixture.GetStreamName();

			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				_fixture.Client.ReadStreamBackwardsAsync(stream, StreamRevision.Start, count, false)
					.ToArrayAsync().AsTask());

			Assert.Equal(nameof(count), ex.ParamName);
		}

		[Fact]
		public async Task stream_does_not_exist_throws() {
			var stream = _fixture.GetStreamName();

			var ex = await Assert.ThrowsAsync<StreamNotFoundException>(() => _fixture.Client
				.ReadStreamBackwardsAsync(stream, StreamRevision.End, 1, false)
				.ToArrayAsync().AsTask());

			Assert.Equal(stream, ex.Stream);
		}

		[Fact]
		public async Task stream_deleted_throws() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.TombstoneAsync(stream, AnyStreamRevision.NoStream);

			var ex = await Assert.ThrowsAsync<StreamDeletedException>(() => _fixture.Client
				.ReadStreamBackwardsAsync(stream, StreamRevision.End, 1, false)
				.ToArrayAsync().AsTask());

			Assert.Equal(stream, ex.Stream);
		}

		[Fact]
		public async Task returns_events_in_reversed_order() {
			var stream = _fixture.GetStreamName();

			var expected = _fixture.CreateTestEvents(10).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, expected);

			var actual = await _fixture.Client
				.ReadStreamBackwardsAsync(stream, StreamRevision.End, expected.Length, false)
				.Select(x => x.Event).ToArrayAsync();

			Assert.True(EventDataComparer.Equal(expected.Reverse().ToArray(),
				actual));
		}

		[Fact]
		public async Task be_able_to_read_single_event_from_arbitrary_position() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents(10).ToArray();

			var expected = events[7];

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);

			var actual = await _fixture.Client.ReadStreamBackwardsAsync(stream, new StreamRevision(7), 1, false)
				.Select(x => x.Event)
				.SingleAsync();

			Assert.True(EventDataComparer.Equal(expected, actual));
		}

		[Fact]
		public async Task be_able_to_read_from_arbitrary_position() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents(10).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);

			var actual = await _fixture.Client.ReadStreamBackwardsAsync(stream, new StreamRevision(3), 2)
				.Select(x => x.Event)
				.ToArrayAsync();

			Assert.True(EventDataComparer.Equal(events.Skip(2).Take(2).Reverse().ToArray(), actual));
		}

		[Fact]
		public async Task be_able_to_read_first_event() {
			var stream = _fixture.GetStreamName();

			var testEvents = _fixture.CreateTestEvents(10).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, testEvents);

			var events = await _fixture.Client.ReadStreamBackwardsAsync(stream, StreamRevision.Start, 1)
				.Select(x => x.Event)
				.ToArrayAsync();

			Assert.Single(events);
			Assert.True(EventDataComparer.Equal(testEvents[0], events[0]));
		}

		[Fact]
		public async Task be_able_to_read_last_event() {
			var stream = _fixture.GetStreamName();

			var testEvents = _fixture.CreateTestEvents(10).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, testEvents);

			var events = await _fixture.Client.ReadStreamBackwardsAsync(stream, StreamRevision.End, 1)
				.Select(x => x.Event)
				.ToArrayAsync();

			Assert.Single(events);
			Assert.True(EventDataComparer.Equal(testEvents[^1], events[0]));
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
