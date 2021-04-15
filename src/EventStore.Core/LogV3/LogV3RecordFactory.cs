using System;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.LogV3 {
	public class LogV3RecordFactory : IRecordFactory<string> {
		private readonly IRecordFactory<string> _logV2RecordFactory;

		public LogV3RecordFactory(IRecordFactory<string> logV2RecordFactory) {
			_logV2RecordFactory = logV2RecordFactory;
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

		// using v2 until v3 prepares are implemented
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

			return _logV2RecordFactory.CreatePrepare(
				logPosition,
				correlationId,
				eventId,
				transactionPosition,
				transactionOffset,
				eventStreamId,
				expectedVersion,
				timeStamp,
				flags,
				eventType,
				data,
				metadata);
		}
	}
}
