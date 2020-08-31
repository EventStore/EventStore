using System;
using EventStore.Core.TransactionLogV2.Chunks.TFChunk;
using EventStore.Core.TransactionLogV2.LogRecords;
using EventStore.Core.TransactionLogV2.TestHelpers;
using EventStore.Core.TransactionLogV2.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.TransactionLogV2.Tests {
	[TestFixture]
	public class when_appending_past_end_of_a_tfchunk : SpecificationWithFile {
		private TFChunk _chunk;
		private readonly Guid _corrId = Guid.NewGuid();
		private readonly Guid _eventId = Guid.NewGuid();
		private bool _written;

		[SetUp]
		public override void SetUp() {
			base.SetUp();
			var record = new PrepareLogRecord(15556, _corrId, _eventId, 15556, 0, "test", 1,
				new DateTime(2000, 1, 1, 12, 0, 0),
				PrepareFlags.None, "Foo", new byte[12], new byte[15]);
			_chunk = TFChunkHelper.CreateNewChunk(Filename, 20);
			_written = _chunk.TryAppend(record).Success;
		}

		[TearDown]
		public override void TearDown() {
			_chunk.Dispose();
			base.TearDown();
		}

		[Test]
		public void the_record_is_not_appended() {
			Assert.IsFalse(_written);
		}
	}
}
