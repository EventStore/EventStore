/*
using System;
using System.IO;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.TransactionLog.MultifileTransactionFile;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_writing_prepare_record_to_file
    {
        private string _tempFileName;
        private MultifileTransactionFileWriter _transactionFileWriter;
        private InMemoryCheckpoint _checkSum;
        private readonly Guid _eventId = Guid.NewGuid();
        private readonly Guid _correlationId = Guid.NewGuid();
        private PrepareLogRecord _record;

        [SetUp]
        public void SetUp()
        {
            _tempFileName = Path.GetTempFileName();
            _checkSum = new InMemoryCheckpoint();
            _transactionFileWriter = new MultifileTransactionFileWriter(_tempFileName, _checkSum);
            _transactionFileWriter.Open();
            _record = new PrepareLogRecord(logPosition: 0xDEAD,
                                           eventId: _eventId,
                                           correlationId: _correlationId,
                                           transactionPosition: 0xDEAD,
                                           eventStreamId: "WorldEnding",
                                           expectedVersion: 1234,
                                           timeStamp: new DateTime(2012, 12, 21),
                // TODO KP to AN : please check if correct replacement for .First | .Last
                                           flags: PrepareFlags.SingleWrite,
                                           eventType: "type",
                                           data: new byte[] { 1, 2, 3, 4, 5 },
                                           metadata: new byte[] {7, 17});
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

                Assert.True(r is PrepareLogRecord);
                var p = (PrepareLogRecord) r;
                Assert.AreEqual(p.RecordType, LogRecordType.Prepare);
                Assert.AreEqual(p.LogPosition, 0xDEAD);
                Assert.AreEqual(p.TransactionPosition, 0xDEAD);
                Assert.AreEqual(p.CorrelationId, _correlationId);
                Assert.AreEqual(p.EventId, _eventId);
                Assert.AreEqual(p.EventStreamId, "WorldEnding");
                Assert.AreEqual(p.ExpectedVersion, 1234);
                Assert.AreEqual(p.TimeStamp, new DateTime(2012, 12, 21));

                // TODO KP to AN : please check if correct replacement for .First | .Last: PrepareFlags.First | PrepareFlags.Last
                Assert.AreEqual(p.Flags, PrepareFlags.SingleWrite);
                Assert.AreEqual(p.EventType, "type");
                Assert.AreEqual(p.Data.Length, 5);
                Assert.AreEqual(p.Metadata.Length, 2);
            }
        }

        [Test]
        public void the_checksum_is_updated()
        {
            Assert.AreEqual(_record.GetSizeWithLengthPrefix(), _checkSum.Read());
        }

        [Test]
        public void trying_to_read_past_writer_checksum_returns_false()
        {
            using (var reader = new TransactionFileReader(_tempFileName, new InMemoryCheckpoint(0)))
            {
                LogRecord record = null;
                reader.Open();
                Assert.IsFalse(reader.TryReadRecordAt(ref record, 5));
            }
        }

        [TearDown]
        public void Teardown()
        {
            _transactionFileWriter.Close();
        }
    }
}
*/
