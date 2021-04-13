using System;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.LogAbstraction {
	public interface IRecordFactory {
		ISystemLogRecord CreateEpoch(EpochRecord epoch);
	}

	public interface IRecordFactory<TStreamId> : IRecordFactory {
		IPrepareLogRecord<TStreamId> CreatePrepare(
			long logPosition,
			Guid correlationId,
			Guid eventId,
			long transactionPosition,
			int transactionOffset,
			TStreamId eventStreamId,
			long expectedVersion,
			DateTime timeStamp,
			PrepareFlags flags,
			string eventType,
			ReadOnlyMemory<byte> data,
			ReadOnlyMemory<byte> metadata);

		IPrepareLogRecord<TStreamId> CopyForRetry(
			IPrepareLogRecord<TStreamId> prepare,
			long logPosition,
			long transactionPosition) {

			var result = CreatePrepare(
				logPosition: logPosition,
				correlationId: prepare.CorrelationId,
				eventId: prepare.EventId,
				transactionPosition: transactionPosition,
				transactionOffset: prepare.TransactionOffset,
				eventStreamId: prepare.EventStreamId,
				expectedVersion: prepare.ExpectedVersion,
				timeStamp: prepare.TimeStamp,
				flags: prepare.Flags,
				eventType: prepare.EventType,
				data: prepare.Data,
				metadata: prepare.Metadata);
			return result;
		}
	}
}
