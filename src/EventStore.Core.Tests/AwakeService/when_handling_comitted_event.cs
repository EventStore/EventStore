using System;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.AwakeService {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_handling_comitted_event<TLogFormat, TStreamId> {
		private Core.Services.AwakeReaderService.AwakeService _it;
		private EventRecord _eventRecord;
		private StorageMessage.EventCommitted _eventCommitted;
		private Exception _exception;

		[SetUp]
		public void SetUp() {
			_exception = null;
			Given();
			When();
		}

		private void Given() {
			_it = new Core.Services.AwakeReaderService.AwakeService();

			var logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;
			logFormat.StreamNameIndex.GetOrAddId("Stream", out var streamId, out _, out _);

			_eventRecord = new EventRecord(
				10,
				LogRecord.Prepare(
					logFormat.RecordFactory, 500, Guid.NewGuid(), Guid.NewGuid(), 500, 0, streamId, 99, PrepareFlags.Data,
					"event", new byte[0], null, DateTime.UtcNow), "Stream");
			_eventCommitted = new StorageMessage.EventCommitted(1000, _eventRecord, isTfEof: true);
		}

		private void When() {
			try {
				_it.Handle(_eventCommitted);
			} catch (Exception ex) {
				_exception = ex;
			}
		}

		[Test]
		public void it_is_handled() {
			Assert.IsNull(_exception, (_exception ?? (object)"").ToString());
		}
	}
}
