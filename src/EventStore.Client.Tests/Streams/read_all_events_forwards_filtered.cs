using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services;
using Xunit;

namespace EventStore.Client.Streams {
	public class read_all_events_forwards_filtered : IClassFixture<read_all_events_forwards_filtered.Fixture> {
		private const string FilteredOutStream = nameof(FilteredOutStream);

		private readonly Fixture _fixture;

		public read_all_events_forwards_filtered(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task regular_expression_stream_name() {
			var streamPrefix = _fixture.GetStreamName();
			var events = _fixture.CreateTestEvents(10).ToArray();

			foreach (var e in events) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] { e });
			}

			var result = await _fixture.Client.ReadAllAsync(Direction.Forwards, Position.Start, 100,
					filterOptions: new FilterOptions(StreamFilter.RegularExpression(new Regex($"^{streamPrefix}"))))
				.ToArrayAsync();

			Assert.Equal(events.Select(x => x.EventId), result.Select(x => x.OriginalEvent.EventId));
		}

		[Fact]
		public async Task prefix_stream_name() {
			var streamPrefix = _fixture.GetStreamName();
			var events = _fixture.CreateTestEvents(10).ToArray();

			foreach (var e in events) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] { e });
			}

			var result = await _fixture.Client.ReadAllAsync(Direction.Forwards, Position.Start, 100,
					filterOptions: new FilterOptions(StreamFilter.Prefix(streamPrefix)))
				.ToArrayAsync();

			Assert.Equal(events.Select(x => x.EventId), result.Select(x => x.OriginalEvent.EventId));
		}

		[Fact]
		public async Task regular_expression_event_type() {
			const string eventTypePrefix = nameof(regular_expression_event_type);
			var streamPrefix = _fixture.GetStreamName();
			var events = _fixture.CreateTestEvents(10)
				.Select(e =>
					new EventData(e.EventId, $"{eventTypePrefix}-{Guid.NewGuid():n}", e.Data, e.Metadata, e.ContentType))
				.ToArray();

			foreach (var e in events) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] { e });
			}

			var result = await _fixture.Client.ReadAllAsync(Direction.Forwards, Position.Start, 100,
					filterOptions: new FilterOptions(EventTypeFilter.RegularExpression(new Regex($"^{eventTypePrefix}"))))
				.ToArrayAsync();

			Assert.Equal(events.Select(x => x.EventId), result.Select(x => x.OriginalEvent.EventId));
		}

		[Fact]
		public async Task prefix_event_type() {
			const string eventTypePrefix = nameof(prefix_event_type);
			var streamPrefix = _fixture.GetStreamName();
			var events = _fixture.CreateTestEvents(10)
				.Select(e =>
					new EventData(e.EventId, $"{eventTypePrefix}-{Guid.NewGuid():n}", e.Data, e.Metadata, e.ContentType))
				.ToArray();

			foreach (var e in events) {
				await _fixture.Client.AppendToStreamAsync($"{streamPrefix}_{Guid.NewGuid():n}",
					AnyStreamRevision.NoStream, new[] { e });
			}

			var result = await _fixture.Client.ReadAllAsync(Direction.Forwards, Position.Start, 100,
					filterOptions: new FilterOptions(EventTypeFilter.Prefix(eventTypePrefix)))
				.ToArrayAsync();

			Assert.Equal(events.Select(x => x.EventId), result.Select(x => x.OriginalEvent.EventId));
		}

		[Theory, InlineData(20), InlineData(2)]
		public async Task max_count_is_respected(int count) {
			var streamName = $"{_fixture.GetStreamName()}_{count}";
			var maxCount = (ulong)count / 2;

			await _fixture.Client.AppendToStreamAsync(streamName, AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents(count));

			var events = await _fixture.Client.ReadAllAsync(Direction.Forwards, Position.Start, maxCount,
					filterOptions: new FilterOptions(EventTypeFilter.ExcludeSystemEvents(40)), 
					userCredentials: TestCredentials.Root)
				.Take(count)
				.ToArrayAsync();

			Assert.Equal(maxCount, (ulong)events.Length);
		}

		[Fact]
		public async Task checkpoint_reached() {
			var streamName = _fixture.GetStreamName();
			var checkpointReached = new TaskCompletionSource<Position>();

			await _fixture.Client.AppendToStreamAsync(FilteredOutStream, AnyStreamRevision.Any,
				_fixture.CreateTestEvents(100));

			var writeResult = await _fixture.Client.AppendToStreamAsync(streamName, AnyStreamRevision.Any,
				_fixture.CreateTestEvents());

			var events = await _fixture.Client.ReadAllAsync(Direction.Forwards, Position.Start, int.MaxValue,
					filterOptions: new FilterOptions(StreamFilter.Prefix(streamName), CheckpointReached))
				.ToArrayAsync();

			Assert.Single(events);

			var position = await checkpointReached.Task.WithTimeout();
			Assert.True(position > Position.Start);
			Assert.True(position < writeResult.LogPosition);

			Task CheckpointReached(Position position, CancellationToken ct) {
				checkpointReached.TrySetResult(position);
				return Task.CompletedTask;
			}
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Client.SetStreamMetadataAsync("$all", AnyStreamRevision.Any,
				new StreamMetadata(acl: new StreamAcl(SystemRoles.All)), userCredentials: TestCredentials.Root);

			protected override Task When() =>
				Client.AppendToStreamAsync(FilteredOutStream, AnyStreamRevision.NoStream, CreateTestEvents(50));
		}
	}
}
