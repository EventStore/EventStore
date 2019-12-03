using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.Streams {
	[Trait("Category", "Network")]
	public class append_to_stream : IClassFixture<append_to_stream.Fixture> {
		private readonly Fixture _fixture;

		public append_to_stream(Fixture fixture) {
			_fixture = fixture;
		}

		public static IEnumerable<object[]> ExpectedVersionCreateStreamTestCases() {
			yield return new object[] {AnyStreamRevision.Any, nameof(AnyStreamRevision.Any)};
			yield return new object[] {AnyStreamRevision.NoStream, nameof(AnyStreamRevision.NoStream)};
		}

		[Theory, MemberData(nameof(ExpectedVersionCreateStreamTestCases))]
		public async Task appending_zero_events(AnyStreamRevision expectedVersion, string name) {
			var stream = $"{_fixture.GetStreamName()}_{name}";

			const int iterations = 2;
			for (var i = 0; i < iterations; i++) {
				var writeResult = await _fixture.Client.AppendToStreamAsync(
					stream, expectedVersion, Enumerable.Empty<EventData>());
				Assert.Equal(AnyStreamRevision.NoStream.ToInt64(), writeResult.NextExpectedVersion);
			}

			var ex = await Assert.ThrowsAsync<StreamNotFoundException>(() =>
				_fixture.Client
					.ReadStreamForwardsAsync(stream, StreamRevision.Start, iterations, false)
					.ToArrayAsync().AsTask());

			Assert.Equal(stream, ex.Stream);
		}

		[Theory, MemberData(nameof(ExpectedVersionCreateStreamTestCases))]
		public async Task appending_zero_events_again(AnyStreamRevision expectedVersion, string name) {
			var stream = $"{_fixture.GetStreamName()}_{name}";

			const int iterations = 2;
			for (var i = 0; i < iterations; i++) {
				var writeResult = await _fixture.Client.AppendToStreamAsync(
					stream, expectedVersion, Enumerable.Empty<EventData>());
				Assert.Equal(AnyStreamRevision.NoStream.ToInt64(), writeResult.NextExpectedVersion);
			}

			var ex = await Assert.ThrowsAsync<StreamNotFoundException>(() =>
				_fixture.Client
					.ReadStreamForwardsAsync(stream, StreamRevision.Start, iterations, false)
					.ToArrayAsync().AsTask());

			Assert.Equal(stream, ex.Stream);
		}

		[Theory, MemberData(nameof(ExpectedVersionCreateStreamTestCases))]
		public async Task create_stream_expected_version_on_first_write_if_does_not_exist(
			AnyStreamRevision expectedVersion,
			string name) {
			var stream = $"{_fixture.GetStreamName()}_{name}";

			var writeResult = await _fixture.Client.AppendToStreamAsync(
				stream,
				expectedVersion,
				_fixture.CreateTestEvents(1));

			Assert.Equal(0, writeResult.NextExpectedVersion);

			var count = await _fixture.Client.ReadStreamForwardsAsync(stream, StreamRevision.Start, 2, false)
				.CountAsync();
			Assert.Equal(1, count);
		}

		[Fact]
		public async Task multiple_idempotent_writes() {
			var stream = _fixture.GetStreamName();
			var events = _fixture.CreateTestEvents(4).ToArray();

			var writeResult = await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, events);
			Assert.Equal(3, writeResult.NextExpectedVersion);

			writeResult = await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, events);
			Assert.Equal(3, writeResult.NextExpectedVersion);
		}

		[Fact]
		public async Task multiple_idempotent_writes_with_same_id_bug_case() {
			var stream = _fixture.GetStreamName();

			var evnt = _fixture.CreateTestEvents().First();
			var events = new[] {evnt, evnt, evnt, evnt, evnt, evnt};

			var writeResult = await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, events);

			Assert.Equal(5, writeResult.NextExpectedVersion);
		}

		[Fact]
		public async Task
			in_case_where_multiple_writes_of_multiple_events_with_the_same_ids_using_expected_version_any_then_next_expected_version_is_unreliable() {
			var stream = _fixture.GetStreamName();

			var evnt = _fixture.CreateTestEvents().First();
			var events = new[] {evnt, evnt, evnt, evnt, evnt, evnt};

			var writeResult = await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, events);

			Assert.Equal(5, writeResult.NextExpectedVersion);

			writeResult = await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, events);

			Assert.Equal(0, writeResult.NextExpectedVersion);
		}

		[Fact]
		public async Task
			in_case_where_multiple_writes_of_multiple_events_with_the_same_ids_using_expected_version_nostream_then_next_expected_version_is_correct() {
			var stream = _fixture.GetStreamName();

			var evnt = _fixture.CreateTestEvents().First();
			var events = new[] {evnt, evnt, evnt, evnt, evnt, evnt};

			var writeResult = await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);

			Assert.Equal(events.Length - 1, writeResult.NextExpectedVersion);

			writeResult = await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);

			Assert.Equal(events.Length - 1, writeResult.NextExpectedVersion);
		}

		[Fact]
		public async Task writing_with_correct_expected_version_to_deleted_stream_throws_stream_deleted() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.TombstoneAsync(stream, AnyStreamRevision.NoStream);

			await Assert.ThrowsAsync<StreamDeletedException>(() => _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents(1)));
		}

		[Fact]
		public async Task returns_log_position_when_writing() {
			var stream = _fixture.GetStreamName();

			var result = await _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents(1));
			Assert.True(0 < result.LogPosition.PreparePosition);
			Assert.True(0 < result.LogPosition.CommitPosition);
		}

		[Fact]
		public async Task writing_with_any_expected_version_to_deleted_stream_throws_stream_deleted() {
			var stream = _fixture.GetStreamName();
			await _fixture.Client.TombstoneAsync(stream, AnyStreamRevision.NoStream);

			await Assert.ThrowsAsync<StreamDeletedException>(
				() => _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, _fixture.CreateTestEvents(1)));
		}

		[Fact]
		public async Task writing_with_invalid_expected_version_to_deleted_stream_throws_stream_deleted() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.TombstoneAsync(stream, AnyStreamRevision.NoStream);

			await Assert.ThrowsAsync<StreamDeletedException>(
				() => _fixture.Client.AppendToStreamAsync(stream, new StreamRevision(5), _fixture.CreateTestEvents()));
		}

		[Fact]
		public async Task append_with_correct_expected_version_to_existing_stream() {
			var stream = _fixture.GetStreamName();

			var writeResult = await _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents(1));

			writeResult = await _fixture.Client.AppendToStreamAsync(
				stream,
				StreamRevision.FromInt64(writeResult.NextExpectedVersion),
				_fixture.CreateTestEvents());

			Assert.Equal(1, writeResult.NextExpectedVersion);
		}

		[Fact]
		public async Task append_with_any_expected_version_to_existing_stream() {
			var stream = _fixture.GetStreamName();

			var writeResult = await _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents(1));

			Assert.Equal(0, writeResult.NextExpectedVersion);

			writeResult = await _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.Any,
				_fixture.CreateTestEvents(1));

			Assert.Equal(1, writeResult.NextExpectedVersion);
		}

		[Fact]
		public async Task appending_with_wrong_expected_version_to_existing_stream_throws_wrong_expected_version() {
			var stream = _fixture.GetStreamName();

			var ex = await Assert.ThrowsAsync<WrongExpectedVersionException>(
				() => _fixture.Client.AppendToStreamAsync(stream, new StreamRevision(1), _fixture.CreateTestEvents()));
			Assert.Equal(1, ex.ExpectedVersion);
			Assert.Equal(AnyStreamRevision.NoStream.ToInt64(), ex.ActualVersion);
		}

		[Fact]
		public async Task append_with_stream_exists_expected_version_to_existing_stream() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, _fixture.CreateTestEvents());

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.StreamExists,
				_fixture.CreateTestEvents());
		}

		[Fact]
		public async Task append_with_stream_exists_expected_version_to_stream_with_multiple_events() {
			var stream = _fixture.GetStreamName();

			for (var i = 0; i < 5; i++) {
				await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, _fixture.CreateTestEvents(1));
			}

			await _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.StreamExists,
				_fixture.CreateTestEvents());
		}

		[Fact]
		public async Task append_with_stream_exists_expected_version_if_metadata_stream_exists() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.SetStreamMetadataAsync(stream, AnyStreamRevision.Any,
				new StreamMetadata(10, default));

			await _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.StreamExists,
				_fixture.CreateTestEvents());
		}

		[Fact]
		public async Task
			appending_with_stream_exists_expected_version_and_stream_does_not_exist_throws_wrong_expected_version() {
			var stream = _fixture.GetStreamName();

			var ex = await Assert.ThrowsAsync<WrongExpectedVersionException>(
				() => _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.StreamExists,
					_fixture.CreateTestEvents()));

			Assert.Equal(AnyStreamRevision.StreamExists.ToInt64(), ex.ExpectedVersion);
			Assert.Equal(AnyStreamRevision.NoStream.ToInt64(), ex.ActualVersion);
		}

		[Fact]
		public async Task appending_with_stream_exists_expected_version_to_hard_deleted_stream_throws_stream_deleted() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.TombstoneAsync(stream, AnyStreamRevision.NoStream);

			await Assert.ThrowsAsync<StreamDeletedException>(() => _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.StreamExists,
				_fixture.CreateTestEvents()));
		}

		[Fact]
		public async Task appending_with_stream_exists_expected_version_to_soft_deleted_stream_throws_stream_deleted() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.SoftDeleteAsync(stream, AnyStreamRevision.NoStream);

			await Assert.ThrowsAsync<StreamDeletedException>(() => _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.StreamExists,
				_fixture.CreateTestEvents()));
		}

		[Fact]
		public async Task can_append_multiple_events_at_once() {
			var stream = _fixture.GetStreamName();

			var writeResult = await _fixture.Client.AppendToStreamAsync(
				stream, AnyStreamRevision.NoStream, _fixture.CreateTestEvents(100));

			Assert.Equal(99, writeResult.NextExpectedVersion);
		}

		[Fact]
		public async Task returns_failure_status_when_conditionally_appending_with_version_mismatch() {
			var stream = _fixture.GetStreamName();

			var result = await _fixture.Client.ConditionalAppendToStreamAsync(stream, new StreamRevision(7),
				_fixture.CreateTestEvents());

			Assert.Equal(
				ConditionalWriteResult.FromWrongExpectedVersion(new WrongExpectedVersionException(stream, 7, -1)),
				result);
		}

		[Fact]
		public async Task returns_success_status_when_conditionally_appending_with_matching_version() {
			var stream = _fixture.GetStreamName();

			var result = await _fixture.Client.ConditionalAppendToStreamAsync(stream, AnyStreamRevision.Any,
				_fixture.CreateTestEvents());

			Assert.Equal(ConditionalWriteResult.FromWriteResult(new WriteResult(0, result.LogPosition)),
				result);
		}

		[Fact]
		public async Task returns_failure_status_when_conditionally_appending_to_a_deleted_stream() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, _fixture.CreateTestEvents());

			await _fixture.Client.TombstoneAsync(stream, AnyStreamRevision.Any);

			var result = await _fixture.Client.ConditionalAppendToStreamAsync(stream, AnyStreamRevision.Any,
				_fixture.CreateTestEvents());

			Assert.Equal(ConditionalWriteResult.StreamDeleted, result);
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
