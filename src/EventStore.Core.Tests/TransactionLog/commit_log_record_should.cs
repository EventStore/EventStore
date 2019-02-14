using System;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog {
	[TestFixture]
	public class commit_log_record_should {
		[Test]
		public void throw_argumentoutofrangeexception_when_given_negative_logposition() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				new CommitLogRecord(-1, Guid.Empty, 0, DateTime.UtcNow, 0);
			});
		}

		[Test]
		public void throw_argumentexception_when_given_empty_correlationid() {
			Assert.Throws<ArgumentException>(() => { new CommitLogRecord(0, Guid.Empty, 0, DateTime.UtcNow, 0); });
		}

		[Test]
		public void throw_argumentoutofrangeexception_when_given_negative_preparestartposition() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				new CommitLogRecord(0, Guid.NewGuid(), -1, DateTime.UtcNow, 0);
			});
		}

		[Test]
		public void throw_argumentoutofrangeexception_when_given_negative_eventversion() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				new CommitLogRecord(0, Guid.NewGuid(), 0, DateTime.UtcNow, -1);
			});
		}
	}
}
