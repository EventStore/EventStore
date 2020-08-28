﻿using System;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.TransactionLog.TestHelpers;
using EventStore.Core.TransactionLog.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.TransactionLog.Tests {
	[TestFixture]
	public class when_reading_an_empty_chunked_transaction_log : SpecificationWithDirectory {
		[Test]
		public void try_read_returns_false_when_writer_checksum_is_zero() {
			var writerchk = new InMemoryCheckpoint(0);
			var chaserchk = new InMemoryCheckpoint(0);
			var db = new TFChunkDb(TFChunkHelper.CreateDbConfig(PathName, writerchk, chaserchk));
			db.Open();

			var reader = new TFChunkReader(db, writerchk, 0);
			Assert.IsFalse(reader.TryReadNext().Success);

			db.Close();
		}

		[Test]
		public void try_read_does_not_cache_anything_and_returns_record_once_it_is_written_later() {
			var writerchk = new InMemoryCheckpoint(0);
			var chaserchk = new InMemoryCheckpoint(0);
			var db = new TFChunkDb(TFChunkHelper.CreateDbConfig(PathName, writerchk, chaserchk));
			db.Open();

			var writer = new TFChunkWriter(db);
			writer.Open();

			var reader = new TFChunkReader(db, writerchk, 0);

			Assert.IsFalse(reader.TryReadNext().Success);

			var rec = LogRecord.SingleWrite(0, Guid.NewGuid(), Guid.NewGuid(), "ES", -1, "ET", new byte[] {7}, null);
			long tmp;
			Assert.IsTrue(writer.Write(rec, out tmp));
			writer.Flush();
			writer.Close();

			var res = reader.TryReadNext();
			Assert.IsTrue(res.Success);
			Assert.AreEqual(rec, res.LogRecord);

			db.Close();
		}
	}
}
