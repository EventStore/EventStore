using System;
using System.Diagnostics;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;

namespace EventStore.Core.LogV2 {
	public class LogV2RecordFactory : IRecordFactory<string> {
		public LogV2RecordFactory() {
		}

		public bool ExplicitStreamCreation => false;
		public bool MultipleEventsPerWrite => false;

		public IPrepareLogRecord<string> CreateStreamRecord(
			Guid streamId,
			long logPosition,
			DateTime timeStamp,
			string streamNumber,
			string streamName) =>
			throw new NotSupportedException();

		public ISystemLogRecord CreateEpoch(EpochRecord epoch) {
			var result = new SystemLogRecord(
				logPosition: epoch.EpochPosition,
				timeStamp: epoch.TimeStamp,
				systemRecordType: SystemRecordType.Epoch,
				systemRecordSerialization: SystemRecordSerialization.Json,
				data: epoch.AsSerialized());
			return result;
		}


		public IPrepareLogRecord<string> CreatePrepare(long logPosition, Guid correlationId, long transactionPosition,
			int transactionOffset, string eventStreamId, long expectedVersion, DateTime timeStamp, PrepareFlags flags,
			IEventRecord[] eventRecords) {
			if (eventRecords.Length > 1) {
				throw new Exception("Multiple event records are not supported");
			}

			var result = new PrepareLogRecord(
				logPosition: logPosition,
				correlationId: correlationId,
				eventId: eventRecords[0].EventId,
				transactionPosition: transactionPosition,
				transactionOffset: transactionOffset,
				eventStreamId: eventStreamId,
				expectedVersion: expectedVersion,
				timeStamp: timeStamp,
				flags: flags,
				eventType: eventRecords[0].EventType,
				data: eventRecords[0].Data,
				metadata: eventRecords[0].Metadata);
			return result;
		}
	}
}
