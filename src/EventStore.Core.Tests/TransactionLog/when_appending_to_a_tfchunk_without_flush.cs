using System;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog {
	[TestFixture]
	public class when_appending_to_a_tfchunk_without_flush : SpecificationWithFilePerTestFixture {
		private TFChunk _chunk;
		private readonly Guid _corrId = Guid.NewGuid();
		private readonly Guid _eventId = Guid.NewGuid();
		private RecordWriteResult _result;
		private PrepareLogRecord _record;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			_record = new PrepareLogRecord(0, _corrId, _eventId, 0, 0, "test", 1, new DateTime(2000, 1, 1, 12, 0, 0),
				PrepareFlags.None, "Foo", new byte[12], new byte[15]);
			_chunk = TFChunkHelper.CreateNewChunk(Filename);
			_result = _chunk.TryAppend(_record);
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			_chunk.Dispose();
			base.TestFixtureTearDown();
		}

		[Test]
		public void the_record_is_appended() {
			Assert.IsTrue(_result.Success);
		}

		[Test]
		public void the_old_position_is_returned() {
			//position without header.
			Assert.AreEqual(0, _result.OldPosition);
		}

		[Test]
		public void the_updated_position_is_returned() {
			//position without header.
			Assert.AreEqual(_record.GetSizeWithLengthPrefixAndSuffix(), _result.NewPosition);
		}
	}
}
