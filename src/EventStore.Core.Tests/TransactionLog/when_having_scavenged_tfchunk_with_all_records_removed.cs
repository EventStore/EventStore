using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_having_scavenged_tfchunk_with_all_records_removed<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture {
		private LogFormatAbstractor<TStreamId> _logFormat;
		private TFChunkDb _db;
		private TFChunk _scavengedChunk;
		private int _originalFileSize;
		private IPrepareLogRecord _p1, _p2, _p3;
		private CommitLogRecord _c1, _c2, _c3;
		private RecordWriteResult _res1, _res2, _res3;
		private RecordWriteResult _cres1, _cres2, _cres3;

		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			_logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormatFactory.Create(new() {
				IndexDirectory = GetFilePathFor("index"),
			});

			_db = new TFChunkDb(TFChunkHelper.CreateSizedDbConfig(PathName, 0, chunkSize: 16 * 1024));
			_db.Open();

			var chunk = _db.Manager.GetChunkFor(0);
			var streamName = "es-to-scavenge";
			var pos = 0L;
			_logFormat.StreamNameIndex.GetOrReserve(_logFormat.RecordFactory, streamName, 0, out var streamId, out var streamRecord);
			if (streamRecord is not null) {
				var res = chunk.TryAppend(streamRecord);
				pos = res.NewPosition;
			}
			var expectedVersion = ExpectedVersion.NoStream;
			_p1 = LogRecord.SingleWrite(_logFormat.RecordFactory, pos, Guid.NewGuid(), Guid.NewGuid(), streamId, expectedVersion++, "et1",
				new byte[2048], new byte[] { 5, 7 });
			_res1 = chunk.TryAppend(_p1);

			_c1 = LogRecord.Commit(_res1.NewPosition, Guid.NewGuid(), _p1.LogPosition, 0);
			_cres1 = chunk.TryAppend(_c1);

			_p2 = LogRecord.SingleWrite(_logFormat.RecordFactory, _cres1.NewPosition,
				Guid.NewGuid(), Guid.NewGuid(), streamId, expectedVersion++, "et1",
				new byte[2048], new byte[] { 5, 7 });
			_res2 = chunk.TryAppend(_p2);

			_c2 = LogRecord.Commit(_res2.NewPosition, Guid.NewGuid(), _p2.LogPosition, 1);
			_cres2 = chunk.TryAppend(_c2);

			_p3 = LogRecord.SingleWrite(_logFormat.RecordFactory, _cres2.NewPosition,
				Guid.NewGuid(), Guid.NewGuid(), streamId, expectedVersion++, "et1",
				new byte[2048], new byte[] { 5, 7 });
			_res3 = chunk.TryAppend(_p3);

			_c3 = LogRecord.Commit(_res3.NewPosition, Guid.NewGuid(), _p3.LogPosition, 2);
			_cres3 = chunk.TryAppend(_c3);

			chunk.Complete();
			_originalFileSize = chunk.FileSize;

			_db.Config.WriterCheckpoint.Write(chunk.ChunkHeader.ChunkEndPosition);
			_db.Config.WriterCheckpoint.Flush();
			_db.Config.ChaserCheckpoint.Write(chunk.ChunkHeader.ChunkEndPosition);
			_db.Config.ChaserCheckpoint.Flush();

			var scavenger = new TFChunkScavenger<TStreamId>(_db, new FakeTFScavengerLog(), new FakeTableIndex<TStreamId>(),
				new FakeReadIndex<TLogFormat, TStreamId>(x => EqualityComparer<TStreamId>.Default.Equals(x, streamId), _logFormat.Metastreams),
				_logFormat.Metastreams);
			await scavenger.Scavenge(alwaysKeepScavenged: true, mergeChunks: false);

			_scavengedChunk = _db.Manager.GetChunk(0);
		}

		public override Task TestFixtureTearDown() {
			_logFormat?.Dispose();
			_db.Dispose();

			return base.TestFixtureTearDown();
		}

		[Test]
		public void first_record_was_written() {
			Assert.IsTrue(_res1.Success);
			Assert.IsTrue(_cres1.Success);
		}

		[Test]
		public void second_record_was_written() {
			Assert.IsTrue(_res2.Success);
			Assert.IsTrue(_cres2.Success);
		}

		[Test]
		public void third_record_was_written() {
			Assert.IsTrue(_res3.Success);
			Assert.IsTrue(_cres3.Success);
		}

		[Test]
		public void prepare1_cant_be_read_at_position() {
			var res = _scavengedChunk.TryReadAt((int)_p1.LogPosition);
			Assert.IsFalse(res.Success);
		}

		[Test]
		public void commit1_cant_be_read_at_position() {
			var res = _scavengedChunk.TryReadAt((int)_c1.LogPosition);
			Assert.IsFalse(res.Success);
		}

		[Test]
		public void prepare2_cant_be_read_at_position() {
			var res = _scavengedChunk.TryReadAt((int)_p2.LogPosition);
			Assert.IsFalse(res.Success);
		}

		[Test]
		public void commit2_cant_be_read_at_position() {
			var res = _scavengedChunk.TryReadAt((int)_c2.LogPosition);
			Assert.IsFalse(res.Success);
		}

		[Test]
		public void prepare3_cant_be_read_at_position() {
			var res = _scavengedChunk.TryReadAt((int)_p3.LogPosition);
			Assert.IsFalse(res.Success);
		}

		[Test]
		public void commit3_cant_be_read_at_position() {
			var res = _scavengedChunk.TryReadAt((int)_c3.LogPosition);
			Assert.IsFalse(res.Success);
		}

		[Test]
		public void sequencial_read_returns_no_records() {
			var records = new List<ILogRecord>();
			RecordReadResult res = _scavengedChunk.TryReadFirst();
			while (res.Success) {
				records.Add(res.LogRecord);
				res = _scavengedChunk.TryReadClosestForward((int)res.NextPosition);
			}

			if (LogFormatHelper<TLogFormat, TStreamId>.IsV2) {
				Assert.AreEqual(0, records.Count);
			}
			else {
				Assert.AreEqual(1, records.Count);
			}
		}

		[Test]
		public void scavenged_chunk_should_have_saved_space() {
			Assert.IsTrue(_scavengedChunk.FileSize < _originalFileSize,
				String.Format("Expected scavenged file size ({0}) to be less than original file size ({1})",
					_scavengedChunk.FileSize, _originalFileSize));
		}

		[Test]
		public void scavenged_chunk_should_be_aligned() {
			Assert.IsTrue(_scavengedChunk.FileSize % 4096 == 0);
		}
	}
}
