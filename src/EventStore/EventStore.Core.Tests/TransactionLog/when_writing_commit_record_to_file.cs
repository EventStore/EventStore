/*using System;
using System.IO;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_writing_commit_record_to_file
    {
        private string _tempFileName;
        private TransactionFileWriter _transactionFileWriter;
        private InMemoryCheckpoint _checkSum;
        private readonly Guid _eventId = Guid.NewGuid();
        private CommitLogRecord _record;

        [SetUp]
        public void SetUp()
        {
            _tempFileName = Path.GetTempFileName();
            _checkSum = new InMemoryCheckpoint();
            _transactionFileWriter = new TransactionFileWriter(_tempFileName, _checkSum);
            _transactionFileWriter.Open();
            _record = new CommitLogRecord(logPosition: 0xFEED,
                                          correlationId: _eventId,
                                          startPosition: 4321,
                                          timeStamp: new DateTime(2012, 12, 21),
                                          eventNumber: 10);
            _transactionFileWriter.Write(_record);
            _transactionFileWriter.Flush();
        }

        [Test]
        public void the_data_is_written()
        {
            //TODO MAKE THIS ACTUALLY ASSERT OFF THE FILE AND READER FROM KNOWN FILE
            using (var reader = new TransactionFileReader(_tempFileName, _checkSum))
            {
                reader.Open();
                LogRecord r = null;
                Assert.IsTrue(reader.TryReadRecord(ref r));

                Assert.True(r is CommitLogRecord);
                var c = (CommitLogRecord) r;
                Assert.AreEqual(c.RecordType, LogRecordType.Commit);
                Assert.AreEqual(c.LogPosition, 0xFEED);
                Assert.AreEqual(c.CorrelationId, _eventId);
                Assert.AreEqual(c.TransactionPosition, 4321);
                Assert.AreEqual(c.TimeStamp, new DateTime(2012, 12, 21));
            }
        }

        [Test]
        public void the_checksum_is_updated()
        {
            Assert.AreEqual(_record.GetSizeWithLengthPrefixAndSuffix(), _checkSum.Read());
        }

        [Test]
        public void trying_to_read_past_writer_checksum_returns_false()
        {
            using (var reader = new TransactionFileReader(_tempFileName, new InMemoryCheckpoint(0)))
            {
                LogRecord record = null;
                reader.Open();
                Assert.IsFalse(reader.TryReadAt(ref record, 5));
            }
        }

        [TearDown]
        public void Teardown()
        {
            _transactionFileWriter.Close();
        }
    }
}*/