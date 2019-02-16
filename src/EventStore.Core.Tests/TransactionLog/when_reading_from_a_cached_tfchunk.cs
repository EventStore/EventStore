using System;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog {
	[TestFixture]
	public class when_reading_from_a_cached_tfchunk : SpecificationWithFilePerTestFixture {
		private TFChunk _chunk;
		private readonly Guid _corrId = Guid.NewGuid();
		private readonly Guid _eventId = Guid.NewGuid();
		private TFChunk _cachedChunk;
		private PrepareLogRecord _record;
		private RecordWriteResult _result;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			_record = new PrepareLogRecord(0, _corrId, _eventId, 0, 0, "test", 1, new DateTime(2000, 1, 1, 12, 0, 0),
				PrepareFlags.None, "Foo", new byte[12], new byte[15]);
			_chunk = TFChunkHelper.CreateNewChunk(Filename);
			_result = _chunk.TryAppend(_record);
			_chunk.Flush();
			_chunk.Complete();
			_cachedChunk = TFChunk.FromCompletedFile(Filename, verifyHash: true, unbufferedRead: false,
				initialReaderCount: 5, reduceFileCachePressure: false);
			_cachedChunk.CacheInMemory();
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			_chunk.Dispose();
			_cachedChunk.Dispose();
			base.TestFixtureTearDown();
		}

		[Test]
		public void the_write_result_is_correct() {
			Assert.IsTrue(_result.Success);
			Assert.AreEqual(0, _result.OldPosition);
			Assert.AreEqual(_record.GetSizeWithLengthPrefixAndSuffix(), _result.NewPosition);
		}

		[Test]
		public void the_chunk_is_cached() {
			Assert.IsTrue(_cachedChunk.IsCached);
		}

		[Test]
		public void the_record_can_be_read_at_exact_position() {
			var res = _cachedChunk.TryReadAt(0);
			Assert.IsTrue(res.Success);
			Assert.AreEqual(_record, res.LogRecord);
			Assert.AreEqual(_result.OldPosition, res.LogRecord.LogPosition);
		}

		[Test]
		public void the_record_can_be_read_as_first_record() {
			var res = _cachedChunk.TryReadFirst();
			Assert.IsTrue(res.Success);
			Assert.AreEqual(_record.GetSizeWithLengthPrefixAndSuffix(), res.NextPosition);
			Assert.AreEqual(_record, res.LogRecord);
			Assert.AreEqual(_result.OldPosition, res.LogRecord.LogPosition);
		}

		[Test]
		public void the_record_can_be_read_as_closest_forward_to_zero_pos() {
			var res = _cachedChunk.TryReadClosestForward(0);
			Assert.IsTrue(res.Success);
			Assert.AreEqual(_record.GetSizeWithLengthPrefixAndSuffix(), res.NextPosition);
			Assert.AreEqual(_record, res.LogRecord);
			Assert.AreEqual(_result.OldPosition, res.LogRecord.LogPosition);
		}

		[Test]
		public void the_record_can_be_read_as_closest_backward_from_end() {
			var res = _cachedChunk.TryReadClosestBackward(_record.GetSizeWithLengthPrefixAndSuffix());
			Assert.IsTrue(res.Success);
			Assert.AreEqual(0, res.NextPosition);
			Assert.AreEqual(_record, res.LogRecord);
		}

		[Test]
		public void the_record_can_be_read_as_last() {
			var res = _cachedChunk.TryReadLast();
			Assert.IsTrue(res.Success);
			Assert.AreEqual(0, res.NextPosition);
			Assert.AreEqual(_record, res.LogRecord);
		}
	}
}
