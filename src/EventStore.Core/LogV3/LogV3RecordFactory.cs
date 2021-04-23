using System;
using EventStore.Common.Utils;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.LogV3 {
	public class LogV3RecordFactory : IRecordFactory<long> {
		public LogV3RecordFactory() {
		}

		public ISystemLogRecord CreateEpoch(EpochRecord epoch) {
			var result = new LogV3EpochLogRecord(
				logPosition: epoch.EpochPosition,
				timeStamp: epoch.TimeStamp,
				epochNumber: epoch.EpochNumber,
				epochId: epoch.EpochId,
				prevEpochPosition: epoch.PrevEpochPosition,
				leaderInstanceId: epoch.LeaderInstanceId);
			return result;
		}

		public IPrepareLogRecord<long> CreatePrepare(
			long logPosition,
			Guid correlationId,
			Guid eventId,
			long transactionPosition,
			int transactionOffset,
			long eventStreamId,
			long expectedVersion,
			DateTime timeStamp,
			PrepareFlags flags,
			string eventType,
			ReadOnlyMemory<byte> data,
			ReadOnlyMemory<byte> metadata) {

			Ensure.Equal(logPosition, transactionPosition, nameof(transactionPosition));

			var result = new LogV3StreamWriteRecord(
				logPosition: logPosition,
				transactionPosition: transactionPosition,
				transactionOffset: transactionOffset,
				correlationId: correlationId,
				eventId: eventId,
				eventStreamId: eventStreamId,
				expectedVersion: expectedVersion,
				timeStamp: timeStamp,
				flags: flags,
				eventType: eventType,
				data: data.Span,
				metadata: metadata.Span);
			return result;
		}
	}
}
