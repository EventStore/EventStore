using EventStore.Core.Data;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Truncation
{
    [TestFixture]
    public class when_truncating_single_uncompleted_chunk_with_index_on_disk : TruncateScenario
    {
        private EventRecord _event0;
        private EventRecord _event1;
        private EventRecord _event2;
        private EventRecord _event3;
        private EventRecord _event4;

        public when_truncating_single_uncompleted_chunk_with_index_on_disk() : base(maxEntriesInMemTable: 3)
        {
        }

        protected override void WriteTestScenario()
        {
            _event0 = WriteStreamCreated("ES");
            _event1 = WriteSingleEvent("ES", 1, new string('.', 500));
            _event2 = WriteSingleEvent("ES", 2, new string('.', 500));
            _event3 = WriteSingleEvent("ES", 3, new string('.', 500));  // index goes to disk
            _event4 = WriteSingleEvent("ES", 4, new string('.', 500));

            TruncateCheckpoint = _event2.LogPosition;
        }

        [Test]
        public void checksums_should_be_equal_to_ack_checksum()
        {
            Assert.AreEqual(TruncateCheckpoint, WriterCheckpoint.Read());
            Assert.AreEqual(TruncateCheckpoint, ChaserCheckpoint.Read());
        }
    }
}
