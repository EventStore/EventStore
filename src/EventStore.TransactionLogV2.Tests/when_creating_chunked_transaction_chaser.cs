using System;
using EventStore.Core.TransactionLogV2.Checkpoint;
using EventStore.Core.TransactionLogV2.Chunks;
using EventStore.Core.TransactionLogV2.TestHelpers;
using EventStore.Core.TransactionLogV2.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.TransactionLogV2.Tests {
	[TestFixture]
	public class when_creating_chunked_transaction_chaser : SpecificationWithDirectory {
		[Test]
		public void a_null_file_config_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(
				() => new TFChunkChaser(null, new InMemoryCheckpoint(0), new InMemoryCheckpoint(0), false));
		}

		[Test]
		public void a_null_writer_checksum_throws_argument_null_exception() {
			var db = new TFChunkDb(TFChunkHelper.CreateDbConfig(PathName, 0));
			Assert.Throws<ArgumentNullException>(() => new TFChunkChaser(db, null, new InMemoryCheckpoint(), false));
		}

		[Test]
		public void a_null_chaser_checksum_throws_argument_null_exception() {
			var db = new TFChunkDb(TFChunkHelper.CreateDbConfig(PathName, 0));
			Assert.Throws<ArgumentNullException>(() => new TFChunkChaser(db, new InMemoryCheckpoint(), null, false));
		}
	}
}
