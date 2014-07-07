using System;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class prepare_log_record_should
    {
        [Test, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void throw_argumentoutofrangeexception_when_given_negative_logposition()
        {
            new PrepareLogRecord(-1, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "test", 0, DateTime.UtcNow,
                                 PrepareFlags.None, "type", new byte[0], null);
        }

        [Test, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void throw_argumentoutofrangeexception_when_given_negative_transactionposition()
        {
            new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), -1, 0, "test", 0, DateTime.UtcNow,
                                 PrepareFlags.None, "type", new byte[0], null);
        }

        [Test, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void throw_argumentoutofrangeexception_when_given_transaction_offset_less_than_minus_one()
        {
            new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, -2, "test", 0, DateTime.UtcNow,
                                 PrepareFlags.None, "type", new byte[0], null);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void throw_argumentexception_when_given_empty_correlationid()
        {
            new PrepareLogRecord(0, Guid.Empty, Guid.NewGuid(), 0, 0, "test", 0, DateTime.UtcNow, 
                                 PrepareFlags.None, "type", new byte[0], null);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void throw_argumentexception_when_given_empty_eventid()
        {
            new PrepareLogRecord(0, Guid.NewGuid(), Guid.Empty, 0, 0, "test", 0, DateTime.UtcNow, 
                                 PrepareFlags.None, "type", new byte[0], null);
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void throw_argumentnullexception_when_given_null_eventstreamid()
        {
            new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, null, 0, DateTime.UtcNow, 
                                 PrepareFlags.None, "type", new byte[0], null);
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void throw_argumentnullexception_when_given_empty_eventstreamid()
        {
            new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, string.Empty, 0, DateTime.UtcNow, 
                                 PrepareFlags.None, "type", new byte[0], null);
        }

        [Test, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void throw_argumentoutofrangeexception_when_given_incorrect_expectedversion()
        {
            new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "test", -3, DateTime.UtcNow, 
                                 PrepareFlags.None, "type", new byte[0], null);
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void throw_argumentnullexception_when_given_null_data()
        {
            new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "test", 0, DateTime.UtcNow, 
                                 PrepareFlags.None, "type", null, null);
        }

        [Test]
        public void throw_argumentnullexception_when_given_null_eventtype()
        {
            Assert.DoesNotThrow(() => 
                new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "test", 0, DateTime.UtcNow, 
                                     PrepareFlags.None, null, new byte[0], null));
        }

        [Test]
        public void throw_argumentexception_when_given_empty_eventtype()
        {
            Assert.DoesNotThrow(() => 
                new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "test", 0, DateTime.UtcNow,
                                     PrepareFlags.None, string.Empty, new byte[0], null));
        }
    }
}