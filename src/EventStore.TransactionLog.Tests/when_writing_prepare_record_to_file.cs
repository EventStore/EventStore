﻿using System;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.TransactionLog.TestHelpers;
using EventStore.Core.TransactionLog.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.TransactionLog.Tests {
	[TestFixture]
	public class when_writing_prepare_record_to_file : SpecificationWithDirectoryPerTestFixture {
		private ITransactionFileWriter _writer;
		private InMemoryCheckpoint _writerCheckpoint;
		private readonly Guid _eventId = Guid.NewGuid();
		private readonly Guid _correlationId = Guid.NewGuid();
		private PrepareLogRecord _record;
		private TFChunkDb _db;

		[OneTimeSetUp]
		public void SetUp() {
			_writerCheckpoint = new InMemoryCheckpoint();
			_db = new TFChunkDb(TFChunkHelper.CreateDbConfig(PathName, _writerCheckpoint, new InMemoryCheckpoint(),
				1024));
			_db.Open();
			_writer = new TFChunkWriter(_db);
			_writer.Open();
			_record = new PrepareLogRecord(logPosition: 0,
				eventId: _eventId,
				correlationId: _correlationId,
				transactionPosition: 0xDEAD,
				transactionOffset: 0xBEEF,
				eventStreamId: "WorldEnding",
				expectedVersion: 1234,
				timeStamp: new DateTime(2012, 12, 21),
				flags: PrepareFlags.SingleWrite,
				eventType: "type",
				data: new byte[] {1, 2, 3, 4, 5},
				metadata: new byte[] {7, 17});
			long newPos;
			_writer.Write(_record, out newPos);
			_writer.Flush();
		}

		[OneTimeTearDown]
		public void Teardown() {
			_writer.Close();
			_db.Close();
		}

		[Test]
		public void the_data_is_written() {
			//TODO MAKE THIS ACTUALLY ASSERT OFF THE FILE AND READER FROM KNOWN FILE
			using (var reader = new TFChunkChaser(_db, _writerCheckpoint, _db.Config.ChaserCheckpoint, false)) {
				reader.Open();
				LogRecord r;
				Assert.IsTrue(reader.TryReadNext(out r));

				Assert.True(r is PrepareLogRecord);
				var p = (PrepareLogRecord)r;
				Assert.AreEqual(p.RecordType, LogRecordType.Prepare);
				Assert.AreEqual(p.LogPosition, 0);
				Assert.AreEqual(p.TransactionPosition, 0xDEAD);
				Assert.AreEqual(p.TransactionOffset, 0xBEEF);
				Assert.AreEqual(p.CorrelationId, _correlationId);
				Assert.AreEqual(p.EventId, _eventId);
				Assert.AreEqual(p.EventStreamId, "WorldEnding");
				Assert.AreEqual(p.ExpectedVersion, 1234);
				Assert.AreEqual(p.TimeStamp, new DateTime(2012, 12, 21));
				Assert.AreEqual(p.Flags, PrepareFlags.SingleWrite);
				Assert.AreEqual(p.EventType, "type");
				Assert.AreEqual(p.Data.Length, 5);
				Assert.AreEqual(p.Metadata.Length, 2);
			}
		}

		[Test]
		public void the_checksum_is_updated() {
			Assert.AreEqual(_record.GetSizeWithLengthPrefixAndSuffix(), _writerCheckpoint.Read());
		}

		[Test]
		public void trying_to_read_past_writer_checksum_returns_false() {
			var reader = new TFChunkReader(_db, _writerCheckpoint);
			Assert.IsFalse(reader.TryReadAt(_writerCheckpoint.Read()).Success);
		}
	}
}
