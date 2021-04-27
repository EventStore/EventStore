using EventStore.Core.Data;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.Metastreams {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class
		read_index_result_original_stream_exists_tests<TLogFormat, TStreamId>
		: SimpleDbTestScenario<TLogFormat, TStreamId> {

		private readonly LogFormatAbstractor<TStreamId> _logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;

		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
			return dbCreator.Chunk(
				Rec.Prepare(0, "existing_stream"),
				Rec.Commit(0, "existing_stream"),
				Rec.Prepare(1, "$existing_stream"),
				Rec.Commit(1, "$existing_stream")
			).CreateDb();
		}

		[Test]
		public void original_stream_exists_is_true_when_reading_metastream_for_existing_stream() {
			_logFormat.StreamNameIndex.GetOrAddId("existing_stream", out var streamId, out _, out _);
			var metaStreamName = SystemStreams.MetastreamOf("existing_stream");
			var metaStreamId = _logFormat.SystemStreams.MetaStreamOf(streamId);
			var read = ReadIndex.ReadEvent(metaStreamName, metaStreamId, 0);
			Assert.True(read.OriginalStreamExists);
		}

		[Test]
		public void original_stream_exists_is_false_when_reading_metastream_for_non_existent_stream() {
			_logFormat.StreamNameIndex.GetOrAddId("non_existent_stream", out var streamId, out _, out _);
			var metaStreamName = SystemStreams.MetastreamOf("non_existent_stream");
			var metaStreamId = _logFormat.SystemStreams.MetaStreamOf(streamId);
			var read = ReadIndex.ReadEvent(metaStreamName, metaStreamId, 0);
			Assert.False(read.OriginalStreamExists);
		}

		[Test]
		public void original_stream_exists_is_null_when_reading_existing_stream() {
			_logFormat.StreamNameIndex.GetOrAddId("existing_stream", out var streamId, out _, out _);
			var read = ReadIndex.ReadEvent("existing_stream", streamId, 0);
			Assert.IsNull(read.OriginalStreamExists);
		}

		[Test]
		public void original_stream_exists_is_null_when_reading_non_existent_stream() {
			_logFormat.StreamNameIndex.GetOrAddId("non_existent_stream", out var streamId, out _, out _);
			var read = ReadIndex.ReadEvent("non_existent_stream", streamId, 0);
			Assert.IsNull(read.OriginalStreamExists);
		}

		[Test]
		public void original_stream_exists_is_true_when_reading_metastream_for_existing_system_stream() {
			_logFormat.StreamNameIndex.GetOrAddId("$existing_stream", out var streamId, out _, out _);
			var metaStreamName = SystemStreams.MetastreamOf("$existing_stream");
			var metaStreamId = _logFormat.SystemStreams.MetaStreamOf(streamId);
			var read = ReadIndex.ReadEvent(metaStreamName, metaStreamId, 0);
			Assert.True(read.OriginalStreamExists);
		}

		[Test]
		public void original_stream_exists_is_false_when_reading_metastream_for_non_existent_system_stream() {
			_logFormat.StreamNameIndex.GetOrAddId("$non_existent_stream", out var streamId, out _, out _);
			var metaStreamName = SystemStreams.MetastreamOf("$non_existent_stream");
			var metaStreamId = _logFormat.SystemStreams.MetaStreamOf(streamId);
			var read = ReadIndex.ReadEvent(metaStreamName, metaStreamId, 0);
			Assert.False(read.OriginalStreamExists);
		}

		[Test]
		public void original_stream_exists_is_null_when_reading_existing_system_stream() {
			_logFormat.StreamNameIndex.GetOrAddId("$existing_stream", out var streamId, out _, out _);
			var read = ReadIndex.ReadEvent("$existing_stream", streamId, 0);
			Assert.IsNull(read.OriginalStreamExists);
		}

		[Test]
		public void original_stream_exists_is_null_when_reading_non_existent_system_stream() {
			_logFormat.StreamNameIndex.GetOrAddId("$non_existent_stream", out var streamId, out _, out _);
			var read = ReadIndex.ReadEvent("$non_existent_stream", streamId, 0);
			Assert.IsNull(read.OriginalStreamExists);
		}
	}
}
