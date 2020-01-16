using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "LongRunning")]
	public class when_having_max_count_set_for_stream : IClassFixture<when_having_max_count_set_for_stream.Fixture> {
		private readonly Fixture _fixture;

		public when_having_max_count_set_for_stream(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task read_stream_forwards_respects_max_count() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.SetStreamMetadataAsync(stream, AnyStreamRevision.NoStream, new StreamMetadata(3));

			var expected = _fixture.CreateTestEvents(5).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, expected);

			var actual = await _fixture.Client.ReadStreamAsync(ReadDirection.Forwards, stream, StreamRevision.Start, 100)
				.Select(x => x.Event)
				.ToArrayAsync();

			Assert.Equal(3, actual.Length);
			Assert.True(EventDataComparer.Equal(expected.Skip(2).ToArray(), actual));
		}

		[Fact]
		public async Task read_stream_backwards_respects_max_count() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.SetStreamMetadataAsync(stream, AnyStreamRevision.NoStream, new StreamMetadata(3));

			var expected = _fixture.CreateTestEvents(5).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, expected);

			var actual = await _fixture.Client.ReadStreamAsync(ReadDirection.Backwards, stream, StreamRevision.End, 100)
				.Select(x => x.Event)
				.ToArrayAsync();

			Assert.Equal(3, actual.Length);
			Assert.True(EventDataComparer.Equal(expected.Skip(2).Reverse().ToArray(), actual));
		}

		[Fact]
		public async Task after_setting_less_strict_max_count_read_stream_forward_reads_more_events() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.SetStreamMetadataAsync(stream, AnyStreamRevision.NoStream, new StreamMetadata(3));

			var expected = _fixture.CreateTestEvents(5).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, expected);

			await _fixture.Client.SetStreamMetadataAsync(stream, StreamRevision.Start, new StreamMetadata(4));

			var actual = await _fixture.Client.ReadStreamAsync(ReadDirection.Forwards, stream, StreamRevision.Start, 100)
				.Select(x => x.Event)
				.ToArrayAsync();

			Assert.Equal(4, actual.Length);
			Assert.True(EventDataComparer.Equal(expected.Skip(1).ToArray(), actual));
		}

		[Fact]
		public async Task after_setting_more_strict_max_count_read_stream_forward_reads_less_events() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.SetStreamMetadataAsync(stream, AnyStreamRevision.NoStream, new StreamMetadata(3));

			var expected = _fixture.CreateTestEvents(5).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, expected);

			await _fixture.Client.SetStreamMetadataAsync(stream, StreamRevision.Start, new StreamMetadata(2));

			var actual = await _fixture.Client.ReadStreamAsync(ReadDirection.Forwards, stream, StreamRevision.Start, 100)
				.Select(x => x.Event)
				.ToArrayAsync();

			Assert.Equal(2, actual.Length);
			Assert.True(EventDataComparer.Equal(expected.Skip(3).ToArray(), actual));
		}

		[Fact]
		public async Task after_setting_less_strict_max_count_read_stream_backwards_reads_more_events() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.SetStreamMetadataAsync(stream, AnyStreamRevision.NoStream, new StreamMetadata(3));

			var expected = _fixture.CreateTestEvents(5).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, expected);

			await _fixture.Client.SetStreamMetadataAsync(stream, StreamRevision.Start, new StreamMetadata(4));

			var actual = await _fixture.Client.ReadStreamAsync(ReadDirection.Backwards, stream, StreamRevision.End, 100)
				.Select(x => x.Event)
				.ToArrayAsync();

			Assert.Equal(4, actual.Length);
			Assert.True(EventDataComparer.Equal(expected.Skip(1).Reverse().ToArray(), actual));
		}

		[Fact]
		public async Task after_setting_more_strict_max_count_read_stream_backwards_reads_less_events() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.SetStreamMetadataAsync(stream, AnyStreamRevision.NoStream, new StreamMetadata(3));

			var expected = _fixture.CreateTestEvents(5).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, expected);

			await _fixture.Client.SetStreamMetadataAsync(stream, StreamRevision.Start, new StreamMetadata(2));

			var actual = await _fixture.Client.ReadStreamAsync(ReadDirection.Backwards, stream, StreamRevision.End, 100)
				.Select(x => x.Event)
				.ToArrayAsync();

			Assert.Equal(2, actual.Length);
			Assert.True(EventDataComparer.Equal(expected.Skip(3).Reverse().ToArray(), actual));
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
