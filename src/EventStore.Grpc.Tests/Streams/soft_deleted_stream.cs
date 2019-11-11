using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.Streams {
	[Trait("Category", "LongRunning")]
	public class soft_deleted_stream : IClassFixture<soft_deleted_stream.Fixture> {
		private readonly Fixture _fixture;
		private readonly JsonDocument _customMetadata;

		public soft_deleted_stream(Fixture fixture) {
			_fixture = fixture;

			var customMetadata = new Dictionary<string, object> {
				["key1"] = true,
				["key2"] = 17,
				["key3"] = "some value"
			};

			_customMetadata = JsonDocument.Parse(JsonSerializer.Serialize(customMetadata));
		}

		[Fact]
		public async Task reading_throws() {
			var stream = _fixture.GetStreamName();

			var writeResult = await _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents());

			Assert.Equal(0, writeResult.NextExpectedVersion);

			await _fixture.Client.SoftDeleteAsync(stream, StreamRevision.FromInt64(writeResult.NextExpectedVersion));

			await Assert.ThrowsAsync<StreamNotFoundException>(
				() => _fixture.Client.ReadStreamForwardsAsync(stream, StreamRevision.Start, int.MaxValue)
					.ToArrayAsync().AsTask());
		}

		public static IEnumerable<object[]> RecreatingTestCases() {
			yield return new object[] {AnyStreamRevision.Any, nameof(AnyStreamRevision.Any)};
			yield return new object[] {AnyStreamRevision.NoStream, nameof(AnyStreamRevision.NoStream)};
		}

		[Theory, MemberData(nameof(RecreatingTestCases))]
		public async Task recreated_with_any_expected_version(
			AnyStreamRevision expectedRevision, string name) {
			var stream = $"{_fixture.GetStreamName()}_{name}";

			var writeResult = await _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents());

			Assert.Equal(0, writeResult.NextExpectedVersion);

			await _fixture.Client.SoftDeleteAsync(stream, StreamRevision.FromInt64(writeResult.NextExpectedVersion));

			var events = _fixture.CreateTestEvents(3).ToArray();

			writeResult = await _fixture.Client.AppendToStreamAsync(stream, expectedRevision, events);

			Assert.Equal(3, writeResult.NextExpectedVersion);

			await Task.Delay(50); //TODO: This is a workaround until github issue #1744 is fixed

			var actual = await _fixture.Client.ReadStreamForwardsAsync(
					stream, StreamRevision.Start, int.MaxValue)
				.Select(x => x.Event)
				.ToArrayAsync();

			Assert.Equal(3, actual.Length);
			Assert.Equal(events.Select(x => x.EventId), actual.Select(x => x.EventId));
			Assert.Equal(
				Enumerable.Range(1, 3).Select(i => new StreamRevision((ulong)i)),
				actual.Select(x => x.EventNumber));

			var metadata = await _fixture.Client.GetStreamMetadataAsync(stream);
			Assert.Equal(new StreamRevision(1), metadata.Metadata.TruncateBefore);
			Assert.Equal(new StreamRevision(1), metadata.MetastreamRevision);
		}

		[Fact]
		public async Task recreated_with_expected_version() {
			var stream = _fixture.GetStreamName();

			var writeResult = await _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents());

			Assert.Equal(0, writeResult.NextExpectedVersion);

			await _fixture.Client.SoftDeleteAsync(stream, StreamRevision.FromInt64(writeResult.NextExpectedVersion));

			var events = _fixture.CreateTestEvents(3).ToArray();

			writeResult = await _fixture.Client.AppendToStreamAsync(stream,
				StreamRevision.FromInt64(writeResult.NextExpectedVersion), events);

			Assert.Equal(3, writeResult.NextExpectedVersion);

			await Task.Delay(50); //TODO: This is a workaround until github issue #1744 is fixed

			var actual = await _fixture.Client.ReadStreamForwardsAsync(
					stream, StreamRevision.Start, int.MaxValue)
				.Select(x => x.Event)
				.ToArrayAsync();

			Assert.Equal(3, actual.Length);
			Assert.Equal(events.Select(x => x.EventId), actual.Select(x => x.EventId));
			Assert.Equal(
				Enumerable.Range(1, 3).Select(i => new StreamRevision((ulong)i)),
				actual.Select(x => x.EventNumber));

			var metadata = await _fixture.Client.GetStreamMetadataAsync(stream);
			Assert.Equal(new StreamRevision(1), metadata.Metadata.TruncateBefore);
			Assert.Equal(new StreamRevision(1), metadata.MetastreamRevision);
		}

		[Fact]
		public async Task recreated_preserves_metadata_except_truncate_before() {
			const int count = 2;
			var stream = _fixture.GetStreamName();

			var writeResult = await _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents(count));

			Assert.Equal(1, writeResult.NextExpectedVersion);

			var streamMetadata = new StreamMetadata(
				acl: new StreamAcl(deleteRole: "some-role"),
				maxCount: 100,
				truncateBefore: new StreamRevision(long.MaxValue), // 1 less than End
				customMetadata: _customMetadata);
			writeResult = await _fixture.Client.SetStreamMetadataAsync(stream, AnyStreamRevision.NoStream,
				streamMetadata);
			Assert.Equal(0, writeResult.NextExpectedVersion);

			var events = _fixture.CreateTestEvents(3).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, new StreamRevision(1), events);

			await Task.Delay(500); //TODO: This is a workaround until github issue #1744 is fixed

			var actual = await _fixture.Client.ReadStreamForwardsAsync(stream, StreamRevision.Start, int.MaxValue)
				.Select(x => x.Event)
				.ToArrayAsync();

			Assert.Equal(3, actual.Length);
			Assert.Equal(events.Select(x => x.EventId), actual.Select(x => x.EventId));
			Assert.Equal(
				Enumerable.Range(count, 3).Select(i => new StreamRevision((ulong)i)),
				actual.Select(x => x.EventNumber));

			var expected = new StreamMetadata(streamMetadata.MaxCount, streamMetadata.MaxAge, new StreamRevision(2),
				streamMetadata.CacheControl, streamMetadata.Acl, streamMetadata.CustomMetadata);
			var metadataResult = await _fixture.Client.GetStreamMetadataAsync(stream);
			Assert.Equal(new StreamRevision(1), metadataResult.MetastreamRevision);
			Assert.Equal(expected, metadataResult.Metadata);
		}

		[Fact]
		public async Task can_be_hard_deleted() {
			var stream = _fixture.GetStreamName();

			var writeResult =
				await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream,
					_fixture.CreateTestEvents(2));

			Assert.Equal(1, writeResult.NextExpectedVersion);

			await _fixture.Client.SoftDeleteAsync(stream, new StreamRevision(1));

			await _fixture.Client.TombstoneAsync(stream, AnyStreamRevision.Any);

			var ex = await Assert.ThrowsAsync<StreamDeletedException>(
				() => _fixture.Client.ReadStreamForwardsAsync(stream, StreamRevision.Start, int.MaxValue)
					.ToArrayAsync().AsTask());

			Assert.Equal(stream, ex.Stream);

			ex = await Assert.ThrowsAsync<StreamDeletedException>(()
				=> _fixture.Client.GetStreamMetadataAsync(stream));

			Assert.Equal(SystemStreams.MetastreamOf(stream), ex.Stream);

			await Assert.ThrowsAsync<StreamDeletedException>(
				() => _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, _fixture.CreateTestEvents()));
		}

		[Fact]
		public async Task allows_recreating_for_first_write_only() {
			var stream = _fixture.GetStreamName();

			var writeResult =
				await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream,
					_fixture.CreateTestEvents(2));

			Assert.Equal(1, writeResult.NextExpectedVersion);

			await _fixture.Client.SoftDeleteAsync(stream, new StreamRevision(1));

			writeResult = await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents(3));

			Assert.Equal(4, writeResult.NextExpectedVersion);

			await Assert.ThrowsAsync<WrongExpectedVersionException>(
				() => _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream,
					_fixture.CreateTestEvents()));
		}

		[Fact]
		public async Task appends_multiple_writes_expected_version_any() {
			var stream = _fixture.GetStreamName();

			var writeResult =
				await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream,
					_fixture.CreateTestEvents(2));

			Assert.Equal(1, writeResult.NextExpectedVersion);

			await _fixture.Client.SoftDeleteAsync(stream, new StreamRevision(1));

			var firstEvents = _fixture.CreateTestEvents(3).ToArray();
			var secondEvents = _fixture.CreateTestEvents(2).ToArray();

			writeResult = await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, firstEvents);

			Assert.Equal(4, writeResult.NextExpectedVersion);

			writeResult = await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, secondEvents);

			Assert.Equal(6, writeResult.NextExpectedVersion);

			var actual = await _fixture.Client.ReadStreamForwardsAsync(stream, StreamRevision.Start, int.MaxValue)
				.Select(x => x.Event)
				.ToArrayAsync();

			Assert.Equal(firstEvents.Concat(secondEvents).Select(x => x.EventId), actual.Select(x => x.EventId));
			Assert.Equal(Enumerable.Range(2, 5).Select(i => new StreamRevision((ulong)i)),
				actual.Select(x => x.EventNumber));

			var metadataResult = await _fixture.Client.GetStreamMetadataAsync(stream);

			Assert.Equal(new StreamRevision(2), metadataResult.Metadata.TruncateBefore);
			Assert.Equal(new StreamRevision(1), metadataResult.MetastreamRevision);
		}

		[Fact]
		public async Task recreated_on_empty_when_metadata_set() {
			const int count = 2;
			var stream = _fixture.GetStreamName();

			var streamMetadata = new StreamMetadata(
				acl: new StreamAcl(deleteRole: "some-role"),
				maxCount: 100,
				truncateBefore: StreamRevision.End,
				customMetadata: _customMetadata);

			await _fixture.Client.SoftDeleteAsync(stream, AnyStreamRevision.NoStream);

			var writeResult = await _fixture.Client.SetStreamMetadataAsync(
				stream,
				StreamRevision.Start,
				streamMetadata);

			Assert.Equal(1, writeResult.NextExpectedVersion);

			await Assert.ThrowsAsync<StreamNotFoundException>(() => _fixture.Client
				.ReadStreamForwardsAsync(stream, StreamRevision.Start, int.MaxValue)
				.ToArrayAsync().AsTask());

			var expected = new StreamMetadata(streamMetadata.MaxCount, streamMetadata.MaxAge, StreamRevision.Start,
				streamMetadata.CacheControl, streamMetadata.Acl, streamMetadata.CustomMetadata);

			var metadataResult = await _fixture.Client.GetStreamMetadataAsync(stream);
			Assert.Equal(new StreamRevision(count), metadataResult.MetastreamRevision);
			Assert.Equal(expected, metadataResult.Metadata);
		}

		[Fact]
		public async Task recreated_on_non_empty_when_metadata_set() {
			const int count = 2;
			var stream = _fixture.GetStreamName();

			var streamMetadata = new StreamMetadata(
				acl: new StreamAcl(deleteRole: "some-role"),
				maxCount: 100,
				customMetadata: _customMetadata);

			var writeResult = await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents(count));

			Assert.Equal(1, writeResult.NextExpectedVersion);

			await _fixture.Client.SoftDeleteAsync(stream, StreamRevision.FromInt64(writeResult.NextExpectedVersion));

			writeResult = await _fixture.Client.SetStreamMetadataAsync(
				stream,
				StreamRevision.Start,
				streamMetadata);

			Assert.Equal(1, writeResult.NextExpectedVersion);

			var actual = await _fixture.Client
				.ReadStreamForwardsAsync(stream, StreamRevision.Start, int.MaxValue)
				.ToArrayAsync();

			Assert.Empty(actual);

			var metadataResult = await _fixture.Client.GetStreamMetadataAsync(stream);
			var expected = new StreamMetadata(streamMetadata.MaxCount, streamMetadata.MaxAge, new StreamRevision(count),
				streamMetadata.CacheControl, streamMetadata.Acl, streamMetadata.CustomMetadata);
			Assert.Equal(expected, metadataResult.Metadata);
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
