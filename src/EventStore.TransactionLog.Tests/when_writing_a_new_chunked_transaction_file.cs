using System;
using System.IO;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.TransactionLog.TestHelpers;
using EventStore.Core.TransactionLog.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.TransactionLog.Tests {
	[TestFixture]
	public class when_writing_a_new_chunked_transaction_file : SpecificationWithDirectory {
		private readonly Guid _eventId = Guid.NewGuid();
		private readonly Guid _correlationId = Guid.NewGuid();
		private InMemoryCheckpoint _checkpoint;

		[Test]
		public void a_record_can_be_written() {
			_checkpoint = new InMemoryCheckpoint(0);
			var db = new TFChunkDb(TFChunkHelper.CreateDbConfig(PathName, _checkpoint, new InMemoryCheckpoint()));
			db.Open();
			var tf = new TFChunkWriter(db);
			tf.Open();
			var record = new PrepareLogRecord(logPosition: 0,
				correlationId: _correlationId,
				eventId: _eventId,
				transactionPosition: 0,
				transactionOffset: 0,
				eventStreamId: "WorldEnding",
				expectedVersion: 1234,
				timeStamp: new DateTime(2012, 12, 21),
				flags: PrepareFlags.None,
				eventType: "type",
				data: new byte[] {1, 2, 3, 4, 5},
				metadata: new byte[] {7, 17});
			long tmp;
			tf.Write(record, out tmp);
			tf.Close();
			db.Dispose();

			Assert.AreEqual(record.GetSizeWithLengthPrefixAndSuffix(), _checkpoint.Read());
			using (var filestream = File.Open(GetFilePathFor("chunk-000000.000000"), FileMode.Open, FileAccess.Read)) {
				filestream.Position = ChunkHeader.Size;

				var reader = new BinaryReader(filestream);
				reader.ReadInt32();
				var read = LogRecord.ReadFrom(reader);
				Assert.AreEqual(record, read);
			}
		}
	}
}
