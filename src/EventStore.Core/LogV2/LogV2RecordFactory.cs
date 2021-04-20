using System;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.LogV2 {
	public class LogV2RecordFactory : IRecordFactory<string> {
		public LogV2RecordFactory() {
		}

		public ISystemLogRecord CreateEpoch(EpochRecord epoch) {
			var result = new SystemLogRecord(
				logPosition: epoch.EpochPosition,
				timeStamp: epoch.TimeStamp,
				systemRecordType: SystemRecordType.Epoch,
				systemRecordSerialization: SystemRecordSerialization.Json,
				data: epoch.AsSerialized());
			return result;
		}

		public IPrepareLogRecord<string> CreatePrepare(
			long logPosition,
			Guid correlationId,
			Guid eventId,
			long transactionPosition,
			int transactionOffset,
			string eventStreamId,
			long expectedVersion,
			DateTime timeStamp,
			PrepareFlags flags,
			string eventType,
			ReadOnlyMemory<byte> data,
			ReadOnlyMemory<byte> metadata) {

			var result = new PrepareLogRecord(
				logPosition: logPosition,
				correlationId: correlationId,
				eventId: eventId,
				transactionPosition: transactionPosition,
				transactionOffset: transactionOffset,
				eventStreamId: eventStreamId,
				expectedVersion: expectedVersion,
				timeStamp: timeStamp,
				flags: flags,
				eventType: eventType,
				data: data,
				metadata: metadata);
			return result;
		}
	}
}
