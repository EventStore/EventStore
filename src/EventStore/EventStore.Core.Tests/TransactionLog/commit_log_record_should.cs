using System;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class commit_log_record_should
    {
        [Test, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void throw_argumentoutofrangeexception_when_given_negative_logposition()
        {
            new CommitLogRecord(-1, Guid.Empty, 0, DateTime.UtcNow, 0);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void throw_argumentexception_when_given_empty_correlationid()
        {
            new CommitLogRecord(0, Guid.Empty, 0, DateTime.UtcNow, 0);
        }

        [Test, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void throw_argumentoutofrangeexception_when_given_negative_preparestartposition()
        {
            new CommitLogRecord(0, Guid.NewGuid(), -1, DateTime.UtcNow, 0);
        }

        [Test, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void throw_argumentoutofrangeexception_when_given_negative_eventversion()
        {
            new CommitLogRecord(0, Guid.NewGuid(), 0, DateTime.UtcNow, -1);
        }
    }
}