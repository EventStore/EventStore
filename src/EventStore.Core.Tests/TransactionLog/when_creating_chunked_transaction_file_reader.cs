using System;
using System.IO;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog {
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
