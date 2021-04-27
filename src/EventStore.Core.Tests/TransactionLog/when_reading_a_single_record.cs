using System;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.LogV2;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_reading_a_single_record<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture {
		private const int RecordsCount = 8;

		private TFChunkDb _db;
		private ILogRecord[] _records;
		private RecordWriteResult[] _results;

		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			_db = new TFChunkDb(TFChunkHelper.CreateSizedDbConfig(PathName, 0, chunkSize: 4096));
			_db.Open();

			var chunk = _db.Manager.GetChunk(0);
			_records = new ILogRecord[RecordsCount];
			_results = new RecordWriteResult[RecordsCount];

			var logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;
			var streamName = "es1";
			logFormat.StreamNameIndex.GetOrAddId(streamName, out var streamId);
			var pos = 0;
			for (int i = 0; i < RecordsCount; ++i) {
				if (i > 0 && i % 3 == 0) {
					pos = i / 3 * _db.Config.ChunkSize;
					chunk.Complete();
					chunk = _db.Manager.AddNewChunk();
				}

				_records[i] = LogRecord.SingleWrite(logFormat.RecordFactory, pos,
					Guid.NewGuid(), Guid.NewGuid(), streamId, ExpectedVersion.Any, "et1",
					new byte[1200], new byte[] { 5, 7 });
				_results[i] = chunk.TryAppend(_records[i]);

				pos += _records[i].GetSizeWithLengthPrefixAndSuffix();
			}

			chunk.Flush();
			_db.Config.WriterCheckpoint.Write((RecordsCount / 3) * _db.Config.ChunkSize +
											  _results[RecordsCount - 1].NewPosition);
			_db.Config.WriterCheckpoint.Flush();
		}

		public override Task TestFixtureTearDown() {
			_db.Dispose();

			return base.TestFixtureTearDown();
		}

		private TFChunkReader GetTFChunkReader(long from) {
			return new TFChunkReader(_db, _db.Config.WriterCheckpoint, from);
		}

		[Test]
		public void all_records_were_written() {
			var pos = 0;
			for (int i = 0; i < RecordsCount; ++i) {
				if (i % 3 == 0)
					pos = 0;

				Assert.IsTrue(_results[i].Success);
				Assert.AreEqual(pos, _results[i].OldPosition);

				pos += _records[i].GetSizeWithLengthPrefixAndSuffix();
				Assert.AreEqual(pos, _results[i].NewPosition);
			}
		}

		[Test]
		public void all_records_can_be_read() {
			var reader = GetTFChunkReader(0);

			RecordReadResult res;
			for (var i = 0; i < RecordsCount; i++) {
				var rec = _records[i];
				res = reader.TryReadAt(rec.LogPosition);

				Assert.IsTrue(res.Success);
				Assert.AreEqual(rec, res.LogRecord);
			}
		}
	}
}
