﻿using System;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.LogAbstraction {
	public interface IRecordFactory {
		ISystemLogRecord CreateEpoch(EpochRecord epoch);
	}

	public interface IRecordFactory<TStreamId> : IRecordFactory {
		bool ExplicitStreamCreation { get; }

		IPrepareLogRecord<TStreamId> CreateStreamRecord(
			Guid streamId,
			long logPosition,
			DateTime timeStamp,
			TStreamId streamNumber,
			string streamName);

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
	}
}
