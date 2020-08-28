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
		when_writing_an_existing_chunked_transaction_file_with_not_enough_space_in_chunk : SpecificationWithDirectory {
		private readonly Guid _correlationId = Guid.NewGuid();
		private readonly Guid _eventId = Guid.NewGuid();
		private InMemoryCheckpoint _checkpoint;

		[Test]
		public void a_record_is_not_written_at_first_but_written_on_second_try() {
			var filename1 = GetFilePathFor("chunk-000000.000000");
			var filename2 = GetFilePathFor("chunk-000001.000000");
			var chunkHeader = new ChunkHeader(TFChunk.CurrentChunkVersion, 10000, 0, 0, false, Guid.NewGuid());
			var chunkBytes = chunkHeader.AsByteArray();
			var bytes = new byte[ChunkHeader.Size + 10000 + ChunkFooter.Size];
			Buffer.BlockCopy(chunkBytes, 0, bytes, 0, chunkBytes.Length);
			File.WriteAllBytes(filename1, bytes);

			_checkpoint = new InMemoryCheckpoint(0);
			var db = new TFChunkDb(TFChunkHelper.CreateDbConfig(PathName, _checkpoint, new InMemoryCheckpoint()));
			db.Open();
			var tf = new TFChunkWriter(db);
			long pos;

			var record1 = new PrepareLogRecord(logPosition: 0,
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
				metadata: new byte[8000]);
			Assert.IsTrue(tf.Write(record1, out pos)); // almost fill up first chunk

			var record2 = new PrepareLogRecord(logPosition: pos,
				correlationId: _correlationId,
				eventId: _eventId,
				expectedVersion: 1234,
				transactionPosition: pos,
				transactionOffset: 0,
				eventStreamId: "WorldEnding",
				timeStamp: new DateTime(2012, 12, 21),
				flags: PrepareFlags.None,
				eventType: "type",
				data: new byte[] {1, 2, 3, 4, 5},
				metadata: new byte[8000]);
			Assert.IsFalse(tf.Write(record2, out pos)); // chunk has too small space

			var record3 = new PrepareLogRecord(logPosition: pos,
				correlationId: _correlationId,
				eventId: _eventId,
				expectedVersion: 1234,
				transactionPosition: pos,
				transactionOffset: 0,
				eventStreamId: "WorldEnding",
				timeStamp: new DateTime(2012, 12, 21),
				flags: PrepareFlags.None,
				eventType: "type",
				data: new byte[] {1, 2, 3, 4, 5},
				metadata: new byte[2000]);
			Assert.IsTrue(tf.Write(record3, out pos));
			tf.Close();
			db.Dispose();

			Assert.AreEqual(record3.GetSizeWithLengthPrefixAndSuffix() + 10000, _checkpoint.Read());
			using (var filestream = File.Open(filename2, FileMode.Open, FileAccess.Read)) {
				filestream.Seek(ChunkHeader.Size + sizeof(int), SeekOrigin.Begin);
				var reader = new BinaryReader(filestream);
				var read = LogRecord.ReadFrom(reader);
				Assert.AreEqual(record3, read);
			}
		}
	}
}
