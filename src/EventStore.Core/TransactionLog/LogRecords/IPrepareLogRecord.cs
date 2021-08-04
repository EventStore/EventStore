using System;
using EventStore.LogCommon;

namespace EventStore.Core.TransactionLog.LogRecords {
	// This interface specifies what the Storage, TF and Index machinery requires
	// in order to handle a prepare (i.e. data) record.
	// The V2 prepare implements it trivially
	public interface IPrepareLogRecord : ILogRecord {
		PrepareFlags Flags { get; }
		long TransactionPosition { get; }
		int TransactionOffset { get; }
		long ExpectedVersion { get; }
		Guid CorrelationId { get; }
		DateTime TimeStamp { get; }
		IEventRecord[] Events { get; }
	}

	public interface IPrepareLogRecord<TStreamId> : IPrepareLogRecord {
		TStreamId EventStreamId { get; }
		IPrepareLogRecord<TStreamId> CopyForRetry(long logPosition, long transactionPosition);
	}
}
