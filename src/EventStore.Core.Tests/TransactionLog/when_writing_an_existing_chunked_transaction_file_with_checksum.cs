using System;
using System.IO;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog {
	[TestFixture]
	public class when_writing_an_existing_chunked_transaction_file_with_checksum : SpecificationWithDirectory {
		private readonly Guid _correlationId = Guid.NewGuid();
		private readonly Guid _eventId = Guid.NewGuid();
		private InMemoryCheckpoint _checkpoint;

		[Test]
		public void a_record_can_be_written() {
			var filename = GetFilePathFor("chunk-000000.000000");
			var chunkHeader = new ChunkHeader(TFChunk.CurrentChunkVersion, 10000, 0, 0, false, chunkId: Guid.NewGuid());
			var chunkBytes = chunkHeader.AsByteArray();
			var bytes = new byte[ChunkHeader.Size + 10000 + ChunkFooter.Size];
			Buffer.BlockCopy(chunkBytes, 0, bytes, 0, chunkBytes.Length);
			File.WriteAllBytes(filename, bytes);

			_checkpoint = new InMemoryCheckpoint(137);
			var db = new TFChunkDb(TFChunkHelper.CreateDbConfig(PathName, _checkpoint, new InMemoryCheckpoint()));
			db.Open();
			var tf = new TFChunkWriter(db);
			var record = new PrepareLogRecord(logPosition: _checkpoint.Read(),
				correlationId: _correlationId,
				eventId: _eventId,
				expectedVersion: 1234,
				transactionPosition: 0,
				transactionOffset: 0,
				eventStreamId: "WorldEnding",
				timeStamp: new DateTime(2012, 12, 21),
				flags: PrepareFlags.None,
				eventType: "type",
				data: new byte[] {1, 2, 3, 4, 5},
				metadata: new byte[] {7, 17});
			long tmp;
			tf.Write(record, out tmp);
			tf.Close();
			db.Dispose();

			Assert.AreEqual(record.GetSizeWithLengthPrefixAndSuffix() + 137,
				_checkpoint.Read()); //137 is fluff assigned to beginning of checkpoint
			using (var filestream = File.Open(filename, FileMode.Open, FileAccess.Read)) {
				filestream.Seek(ChunkHeader.Size + 137 + sizeof(int), SeekOrigin.Begin);
				var reader = new BinaryReader(filestream);
				var read = LogRecord.ReadFrom(reader);
				Assert.AreEqual(record, read);
			}
		}
	}
}
