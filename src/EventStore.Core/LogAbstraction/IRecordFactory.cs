using System;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;

namespace EventStore.Core.LogAbstraction {
	public interface IRecordFactory {
		ISystemLogRecord CreateEpoch(EpochRecord epoch);
	}

	public interface IRecordFactory<TStreamId> : IRecordFactory {
		bool ExplicitStreamCreation { get; }
		bool MultipleEventsPerPrepare { get; }

		IPrepareLogRecord<TStreamId> CreateStreamRecord(
			Guid streamId,
			long logPosition,
			DateTime timeStamp,
			TStreamId streamNumber,
			string streamName);

		IPrepareLogRecord<TStreamId> CreatePrepare(
			long logPosition,
			Guid correlationId,
			long transactionPosition,
			int transactionOffset,
			TStreamId eventStreamId,
			long expectedVersion,
			DateTime timeStamp,
			PrepareFlags flags,
			IEventRecord[] eventRecords);
	}
}
