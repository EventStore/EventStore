using System;
using System.IO;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_closing_the_database<TLogFormat, TStreamId> : SpecificationWithDirectory {

		private TFChunkDb _db;

		private static void CreateChunk(string path, int size) {
			var chunkHeader = new ChunkHeader(TFChunk.CurrentChunkVersion, size, 0, 0, false, Guid.NewGuid());
			var chunkBytes = chunkHeader.AsByteArray();
			var buf = new byte[ChunkHeader.Size + ChunkFooter.Size + chunkHeader.ChunkSize];
			Buffer.BlockCopy(chunkBytes, 0, buf, 0, chunkBytes.Length);
			File.WriteAllBytes(path, buf);
		}

		private static IPrepareLogRecord<TStreamId> CreateRecord() {
			var recordFactory = LogFormatHelper<TLogFormat, TStreamId>.RecordFactory;
			var streamId = LogFormatHelper<TLogFormat, TStreamId>.StreamId;
			var eventTypeId = LogFormatHelper<TLogFormat, TStreamId>.EventTypeId;

			return LogRecord.Prepare(
				factory: recordFactory,
				logPosition: 0,
				correlationId: Guid.NewGuid(),
				eventId: Guid.NewGuid(),
				transactionPos: 0,
				transactionOffset: 0,
				eventStreamId: streamId,
				expectedVersion: -1,
				timeStamp: new DateTime(2012, 12, 21),
				flags: PrepareFlags.SingleWrite,
				eventType: eventTypeId,
				data: new byte[123],
				metadata: new byte[] {0x13, 0x37});
		}

		private static ICheckpoint OpenCheckpoint(string path) =>
			new FileCheckpoint(path, Path.GetFileName(path));

		[SetUp]
		public override Task SetUp() {
			base.SetUp();

			CreateChunk(GetFilePathFor("chunk-000000.000000"), 10_000);

			_db = new TFChunkDb(TFChunkHelper.CreateDbConfig(
				PathName,
				OpenCheckpoint(GetFilePathFor("writer.chk")),
				OpenCheckpoint(GetFilePathFor("chaser.chk")),
				10_000));
			_db.Open();

			return Task.CompletedTask;
		}

		[TestCase(true)]
		[TestCase(false)]
		public void checkpoints_should_be_flushed_only_when_chunks_are_properly_closed(bool chunksClosed) {
			if (!chunksClosed) {
				// acquire a reader to prevent the chunk from being properly closed
				_db.Manager.GetChunk(0).AcquireReader();
			}

			var writer = new TFChunkWriter(_db);
			Assert.IsTrue(writer.Write(CreateRecord(), out _));
			_db.Config.ChaserCheckpoint.Write(_db.Config.WriterCheckpoint.ReadNonFlushed());

			_db.Close();

			// reopen the checkpoints
			var writerChk = OpenCheckpoint(GetFilePathFor("writer.chk"));
			var chaserChk = OpenCheckpoint(GetFilePathFor("chaser.chk"));

			if (chunksClosed) {
				Assert.Greater(writerChk.Read(), 0L);
				Assert.Greater(chaserChk.Read(), 0L);
			} else {
				Assert.AreEqual(0L, writerChk.Read());
				Assert.AreEqual(0L, chaserChk.Read());
			}
		}
	}
}
