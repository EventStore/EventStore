using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EventStore.Core.Services;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "LongRunning")]
	public class read_all_events_forward_with_soft_deleted_stream
		: IClassFixture<read_all_events_forward_with_soft_deleted_stream.Fixture> {
		private const string Stream = "stream";
		private readonly Fixture _fixture;

		public read_all_events_forward_with_soft_deleted_stream(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task reading_soft_deleted_stream_throws_stream_not_found() {
			var ex = await Assert.ThrowsAsync<StreamNotFoundException>(() =>
				_fixture.Client.ReadStreamForwardsAsync(Stream, StreamRevision.Start, 1).CountAsync().AsTask());
			Assert.Equal(Stream, ex.Stream);
		}

		[Fact]
		public async Task returns_all_events_including_tombstone() {
			var events = await _fixture.Client.ReadAllForwardsAsync(Position.Start, (ulong)_fixture.Events.Length * 2)
				.ToArrayAsync();

			var tombstone = events.Last();
			Assert.Equal(SystemStreams.MetastreamOf(Stream), tombstone.Event.EventStreamId);
			Assert.Equal(SystemEventTypes.StreamMetadata, tombstone.Event.EventType);
			var metadata = JsonSerializer.Deserialize<StreamMetadata>(tombstone.Event.Data, new JsonSerializerOptions {
				Converters = { StreamMetadataJsonConverter.Instance }
			});
			Assert.True(metadata.TruncateBefore.HasValue);
			Assert.Equal(StreamRevision.End, metadata.TruncateBefore.Value);

			Assert.True(EventDataComparer.Equal(_fixture.Events, events.AsResolvedTestEvents().ToArray()));
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override async Task Given() {
				var result = await Client.SetStreamMetadataAsync(
					"$all",
					AnyStreamRevision.NoStream,
					new StreamMetadata(acl: new StreamAcl(readRole: "$all")),
					TestCredentials.Root);
				Events = CreateTestEvents(20).ToArray();

				await Client.AppendToStreamAsync(Stream, AnyStreamRevision.NoStream, Events);
				await Client.SoftDeleteAsync(Stream, AnyStreamRevision.Any);
			}

			public EventData[] Events { get; private set; }

			protected override Task When() => Task.CompletedTask;
		}
	}
}
