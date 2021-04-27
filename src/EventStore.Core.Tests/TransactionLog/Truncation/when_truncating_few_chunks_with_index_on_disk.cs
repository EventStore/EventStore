using System.IO;
using EventStore.Core.Data;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Truncation {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_truncating_few_chunks_with_index_on_disk<TLogFormat, TStreamId> : TruncateScenario<TLogFormat, TStreamId> {
		private EventRecord _event4;

		private string _chunk0;
		private string _chunk1;
		private string _chunk2;
		private string _chunk3;

		public when_truncating_few_chunks_with_index_on_disk()
			: base(maxEntriesInMemTable: 3) {
		}

		protected override void WriteTestScenario() {
			WriteSingleEvent("ES", 0, new string('.', 4000));
			WriteSingleEvent("ES", 1, new string('.', 4000));
			WriteSingleEvent("ES", 2, new string('.', 4000), retryOnFail: true); // ptable 1, chunk 1
			_event4 = WriteSingleEvent("ES", 3, new string('.', 4000));
			WriteSingleEvent("ES", 4, new string('.', 4000), retryOnFail: true); // chunk 2
			WriteSingleEvent("ES", 5, new string('.', 4000)); // ptable 2
			WriteSingleEvent("ES", 6, new string('.', 4000), retryOnFail: true); // chunk 3 

			TruncateCheckpoint = _event4.LogPosition;

			_chunk0 = GetChunkName(0);
			_chunk1 = GetChunkName(1);
			_chunk2 = GetChunkName(2);
			_chunk3 = GetChunkName(3);

			Assert.IsTrue(File.Exists(_chunk0));
			Assert.IsTrue(File.Exists(_chunk1));
			Assert.IsTrue(File.Exists(_chunk2));
			Assert.IsTrue(File.Exists(_chunk3));
		}

		private string GetChunkName(int chunkNumber) {
			var allVersions = Db.Config.FileNamingStrategy.GetAllVersionsFor(chunkNumber);
			Assert.AreEqual(1, allVersions.Length);
			return allVersions[0];
		}

		[Test]
		public void checksums_should_be_equal_to_ack_checksum() {
			Assert.AreEqual(TruncateCheckpoint, WriterCheckpoint.Read());
			Assert.AreEqual(TruncateCheckpoint, ChaserCheckpoint.Read());
		}

		[Test]
		public void truncated_chunks_should_be_deleted() {
			Assert.IsFalse(File.Exists(_chunk2));
			Assert.IsFalse(File.Exists(_chunk3));
		}

		[Test]
		public void not_truncated_chunks_should_survive() {
			var chunks = Db.Config.FileNamingStrategy.GetAllPresentFiles();
			Assert.AreEqual(2, chunks.Length);
			Assert.AreEqual(_chunk0, GetChunkName(0));
			Assert.AreEqual(_chunk1, GetChunkName(1));
		}

		[Test]
		public void read_all_returns_only_survived_events() {
		}
	}
}
