using System.IO;
using EventStore.Core.Data;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Truncation
{
    [TestFixture]
    public class when_truncating_few_chunks_with_index_on_disk : TruncateScenario
    {
        private EventRecord _event0;
        private EventRecord _event1;
        private EventRecord _event2;
        private EventRecord _event3;
        private EventRecord _event4;
        private EventRecord _event5;
        private EventRecord _event6;
        private EventRecord _event7;

        private string chunk0;
        private string chunk1;
        private string chunk2;
        private string chunk3;

        public when_truncating_few_chunks_with_index_on_disk() : base(maxEntriesInMemTable: 3)
        {
        }

        protected override void WriteTestScenario()
        {
            _event0 = WriteStreamCreated("ES");                          // chunk 0
            _event1 = WriteSingleEvent("ES", 1, new string('.', 4000));
            _event2 = WriteSingleEvent("ES", 2, new string('.', 4000));
            _event3 = WriteSingleEvent("ES", 3, new string('.', 4000), retryOnFail: true);  // ptable 1, chunk 1
            _event4 = WriteSingleEvent("ES", 4, new string('.', 4000));
            _event5 = WriteSingleEvent("ES", 5, new string('.', 4000), retryOnFail: true);  // chunk 2
            _event6 = WriteSingleEvent("ES", 6, new string('.', 4000));  // ptable 2
            _event7 = WriteSingleEvent("ES", 7, new string('.', 4000), retryOnFail: true);  // chunk 3 

            TruncateCheckpoint = _event4.LogPosition;

            chunk0 = GetChunkName(0);
            chunk1 = GetChunkName(1);
            chunk2 = GetChunkName(2);
            chunk3 = GetChunkName(3);

            Assert.IsTrue(File.Exists(chunk0));
            Assert.IsTrue(File.Exists(chunk1));
            Assert.IsTrue(File.Exists(chunk2));
            Assert.IsTrue(File.Exists(chunk3));
        }

        private string GetChunkName(int chunkNumber)
        {
            var allVersions = Db.Config.FileNamingStrategy.GetAllVersionsFor(chunkNumber);
            Assert.AreEqual(1, allVersions.Length);
            return allVersions[0];
        }

        [Test]
        public void checksums_should_be_equal_to_ack_checksum()
        {
            Assert.AreEqual(TruncateCheckpoint, WriterCheckpoint.Read());
            Assert.AreEqual(TruncateCheckpoint, ChaserCheckpoint.Read());
        }

        [Test]
        public void truncated_chunks_should_be_deleted()
        {
            Assert.IsFalse(File.Exists(chunk2));
            Assert.IsFalse(File.Exists(chunk3));
        } 

        [Test]
        public void not_truncated_chunks_should_survive()
        {
            var chunks = Db.Config.FileNamingStrategy.GetAllPresentFiles();
            Assert.AreEqual(2, chunks.Length);
            Assert.AreEqual(chunk0, GetChunkName(0));
            Assert.AreEqual(chunk1, GetChunkName(1));
        }

        [Test]
        public void read_all_returns_only_survived_events()
        {
        }
    }
}
