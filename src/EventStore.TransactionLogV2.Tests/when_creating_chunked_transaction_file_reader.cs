using System;
using EventStore.Core.TransactionLogV2.Checkpoint;
using EventStore.Core.TransactionLogV2.Chunks;
using EventStore.Core.TransactionLogV2.TestHelpers;
using EventStore.Core.TransactionLogV2.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.TransactionLogV2.Tests {
	[TestFixture]
	public class when_creating_chunked_transaction_file_reader : SpecificationWithDirectory {
		[Test]
		public void a_null_db_config_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() => new TFChunkReader(null, new InMemoryCheckpoint(0)));
		}

		[Test]
		public void a_null_checkpoint_throws_argument_null_exception() {
			var config = TFChunkHelper.CreateDbConfig(PathName, 0);
			var db = new TFChunkDb(config);
			Assert.Throws<ArgumentNullException>(() => new TFChunkReader(db, null));
		}
	}
}
