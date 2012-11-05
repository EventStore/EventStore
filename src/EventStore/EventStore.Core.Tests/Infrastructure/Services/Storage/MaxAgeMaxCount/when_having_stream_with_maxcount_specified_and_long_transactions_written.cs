using System;
using System.Text;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Infrastructure.Services.Storage.MaxAgeMaxCount
{
    [TestFixture]
    public class when_having_stream_with_maxcount_specified_and_long_transactions_written : ReadIndexTestScenario
    {
        private EventRecord[] _records;

        protected override void WriteTestScenario()
        {
            const string metadata = @"{""$maxCount"":2}";

            _records = new EventRecord[10]; // 1 + 3 + 2 + 4
            _records[0] = WriteStreamCreated("ES", metadata);

            WriteTransaction(0, 3);
            WriteTransaction(3, 2);
            WriteTransaction(3 + 2, 4);
        }

        private void WriteTransaction(int expectedVersion, int transactionLength)
        {
            var begin = WriteTransactionBegin("ES", expectedVersion);
            for (int i = 0; i < transactionLength; ++i)
            {
                var eventNumber = expectedVersion + i + 1;
                _records[eventNumber] = WriteTransactionEvent(Guid.NewGuid(), begin.LogPosition, i, "ES", eventNumber, "data" + i, PrepareFlags.Data);
            }
            WriteTransactionEnd(Guid.NewGuid(), begin.LogPosition, "ES");
            WriteCommit(Guid.NewGuid(), begin.LogPosition, "ES", expectedVersion + 1);
        }

        [Test]
        public void forward_range_read_returns_last_transaction_events_and_doesnt_return_expired_ones()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("ES", 0, 100, out records));
            Assert.AreEqual(2, records.Length);
            Assert.AreEqual(_records[8], records[0]);
            Assert.AreEqual(_records[9], records[1]);
        }
    }
}