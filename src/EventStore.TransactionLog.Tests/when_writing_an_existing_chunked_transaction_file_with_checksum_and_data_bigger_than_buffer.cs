using System;
using System.IO;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.TransactionLog.TestHelpers;
using EventStore.Core.TransactionLog.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.TransactionLog.Tests {
	[TestFixture]
	public class
		when_writing_an_existing_chunked_transaction_file_with_checksum_and_data_bigger_than_buffer :
			SpecificationWithDirectory {
		private readonly Guid _correlationId = Guid.NewGuid();
		private readonly Guid _eventId = Guid.NewGuid();
		private InMemoryCheckpoint _checkpoint;

		[Test]
		public void a_record_can_be_written() {
			var filename = GetFilePathFor("chunk-000000.000000");
			var chunkHeader = new ChunkHeader(TFChunk.CurrentChunkVersion, 10000, 0, 0, false, Guid.NewGuid());
			var chunkBytes = chunkHeader.AsByteArray();
			var buf = new byte[ChunkHeader.Size + ChunkFooter.Size + chunkHeader.ChunkSize];
			Buffer.BlockCopy(chunkBytes, 0, buf, 0, chunkBytes.Length);
			File.WriteAllBytes(filename, buf);

			_checkpoint = new InMemoryCheckpoint(137);
			var db = new TFChunkDb(TFChunkHelper.CreateDbConfig(PathName, _checkpoint, new InMemoryCheckpoint(),
				chunkSize: chunkHeader.ChunkSize));
			db.Open();

			var
				bytes = new byte[3994]; // this gives exactly 4097 size of record, with 3993 (rec size 4096) everything works fine!
			new Random().NextBytes(bytes);
			var writer = new TFChunkWriter(db);
			var record = new PrepareLogRecord(logPosition: 137,
				correlationId: _correlationId,
				eventId: _eventId,
				transactionPosition: 789,
				transactionOffset: 543,
				eventStreamId: "WorldEnding",
				expectedVersion: 1234,
				timeStamp: new DateTime(2012, 12, 21),
				flags: PrepareFlags.SingleWrite,
				eventType: "type",
				data: bytes,
				metadata: new byte[] {0x07, 0x17});

			long pos;
			Assert.IsTrue(writer.Write(record, out pos));
			writer.Close();
			db.Dispose();

			Assert.AreEqual(record.GetSizeWithLengthPrefixAndSuffix() + 137, _checkpoint.Read());
			using (var filestream = File.Open(filename, FileMode.Open, FileAccess.Read)) {
				filestream.Seek(ChunkHeader.Size + 137 + sizeof(int), SeekOrigin.Begin);
				var reader = new BinaryReader(filestream);
				var read = LogRecord.ReadFrom(reader);
				Assert.AreEqual(record, read);
			}
		}
	}
}
