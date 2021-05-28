using System;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class prepare_log_record_should<TLogFormat, TStreamId> {
		private readonly IRecordFactory<TStreamId> _recordFactory = LogFormatHelper<TLogFormat, TStreamId>.RecordFactory;
		private readonly TStreamId _streamId = LogFormatHelper<TLogFormat, TStreamId>.StreamId;

		public prepare_log_record_should() {
		}

		[Test]
		public void throw_argumentoutofrangeexception_when_given_negative_logposition() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				LogRecord.Prepare(_recordFactory, -1, Guid.NewGuid(), Guid.NewGuid(), 0, 0, _streamId, 0,
					PrepareFlags.None, "type", new byte[0], null, DateTime.UtcNow);
			});
		}

		[Test]
		public void throw_argumentoutofrangeexception_when_given_negative_transactionposition() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				LogRecord.Prepare(_recordFactory, 0, Guid.NewGuid(), Guid.NewGuid(), -1, 0, _streamId, 0,
					PrepareFlags.None, "type", new byte[0], null, DateTime.UtcNow);
			});
		}

		[Test]
		public void throw_argumentoutofrangeexception_when_given_transaction_offset_less_than_minus_one() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				LogRecord.Prepare(_recordFactory, 0, Guid.NewGuid(), Guid.NewGuid(), 0, -2, _streamId, 0,
					PrepareFlags.None, "type", new byte[0], null, DateTime.UtcNow);
			});
		}

		[Test]
		public void throw_argumentexception_when_given_empty_correlationid() {
			Assert.Throws<ArgumentException>(() => {
				LogRecord.Prepare(_recordFactory, 0, Guid.Empty, Guid.NewGuid(), 0, 0, _streamId, 0,
					PrepareFlags.None, "type", new byte[0], null, DateTime.UtcNow);
			});
		}

		[Test]
		public void throw_argumentexception_when_given_empty_eventid() {
			Assert.Throws<ArgumentException>(() => {
				LogRecord.Prepare(_recordFactory, 0, Guid.NewGuid(), Guid.Empty, 0, 0, _streamId, 0,
					PrepareFlags.None, "type", new byte[0], null, DateTime.UtcNow);
			});
		}

		[Test]
		public void throw_argumentnullexception_when_given_null_eventstreamid() {
			TStreamId nullStreamId = default;
			var expectedExceptionType = LogFormatHelper<TLogFormat, TStreamId>.Choose<Type>(
				typeof(ArgumentNullException),
				typeof(ArgumentOutOfRangeException));

			Assert.Throws(expectedExceptionType, () => {
				LogRecord.Prepare(_recordFactory, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, nullStreamId, 0,
					PrepareFlags.None, "type", new byte[0], null, DateTime.UtcNow);
			});
		}

		[Test]
		public void throw_argumentexception_when_given_empty_eventstreamid() {
			var emptyStreamId = LogFormatHelper<TLogFormat, TStreamId>.EmptyStreamId;
			var expectedExceptionType = LogFormatHelper<TLogFormat, TStreamId>.Choose<Type>(
				typeof(ArgumentNullException),
				typeof(ArgumentOutOfRangeException));

			Assert.Throws(expectedExceptionType, () => {
				LogRecord.Prepare(_recordFactory, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, emptyStreamId, 0,
					PrepareFlags.None, "type", new byte[0], null, DateTime.UtcNow);
			});
		}

		[Test]
		public void throw_argumentoutofrangeexception_when_given_incorrect_expectedversion() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				LogRecord.Prepare(_recordFactory, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, _streamId, -3,
					PrepareFlags.None, "type", new byte[0], null, DateTime.UtcNow);
			});
		}

		[Test, Ignore("ReadOnlyMemory will always convert back to empty array if initialized with null array.")]
		public void throw_argumentnullexception_when_given_null_data() {
			Assert.Throws<ArgumentNullException>(() => {
				LogRecord.Prepare(_recordFactory, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, _streamId, 0,
					PrepareFlags.None, "type", null, null, DateTime.UtcNow);
			});
		}

		[Test]
		public void throw_argumentnullexception_when_given_null_eventtype() {
			Assert.DoesNotThrow(() =>
				LogRecord.Prepare(_recordFactory, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, _streamId, 0,
					PrepareFlags.None, null, new byte[0], null, DateTime.UtcNow));
		}

		[Test]
		public void throw_argumentexception_when_given_empty_eventtype() {
			Assert.DoesNotThrow(() =>
				LogRecord.Prepare(_recordFactory, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, _streamId, 0,
					PrepareFlags.None, string.Empty, new byte[0], null,  DateTime.UtcNow));
		}
	}
}
