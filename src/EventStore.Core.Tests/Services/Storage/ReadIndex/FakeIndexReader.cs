using System;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.Services.Storage.ReadIndex;

class FakeIndexReader<TStreamId> : IIndexReader<TStreamId> {
	public long CachedStreamInfo { get; }
	public long NotCachedStreamInfo { get; }
	public long HashCollisions { get; }

	public IndexReadEventResult ReadEvent(string streamName, TStreamId streamId, long eventNumber) {
		throw new NotImplementedException();
	}

	public IndexReadStreamResult
		ReadStreamEventsForward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount) {
		throw new NotImplementedException();
	}

	public IndexReadStreamResult ReadStreamEventsBackward(string streamName, TStreamId streamId, long fromEventNumber,
		int maxCount) {
		throw new NotImplementedException();
	}

	public StorageMessage.EffectiveAcl GetEffectiveAcl(TStreamId streamId) {
		throw new NotImplementedException();
	}

	public IndexReadEventInfoResult ReadEventInfo_KeepDuplicates(TStreamId streamId, long eventNumber) {
		throw new NotImplementedException();
	}

	public IndexReadEventInfoResult ReadEventInfoForward_KnownCollisions(TStreamId streamId, long fromEventNumber, int maxCount,
		long beforePosition) {
		throw new NotImplementedException();
	}

	public IndexReadEventInfoResult ReadEventInfoForward_NoCollisions(ulong stream, long fromEventNumber, int maxCount,
		long beforePosition) {
		throw new NotImplementedException();
	}

	public IndexReadEventInfoResult ReadEventInfoBackward_KnownCollisions(TStreamId streamId, long fromEventNumber, int maxCount,
		long beforePosition) {
		throw new NotImplementedException();
	}

	public IndexReadEventInfoResult ReadEventInfoBackward_NoCollisions(ulong stream, Func<ulong, TStreamId> getStreamId, long fromEventNumber,
		int maxCount, long beforePosition) {
		throw new NotImplementedException();
	}

	public IPrepareLogRecord<TStreamId> ReadPrepare(TStreamId streamId, long eventNumber) {
		throw new NotImplementedException();
	}

	public TStreamId GetEventStreamIdByTransactionId(long transactionId) {
		throw new NotImplementedException();
	}

	public StreamMetadata GetStreamMetadata(TStreamId streamId) {
		throw new NotImplementedException();
	}

	public long GetStreamLastEventNumber(TStreamId streamId) {
		throw new NotImplementedException();
	}

	public long GetStreamLastEventNumber_KnownCollisions(TStreamId streamId, long beforePosition) {
		throw new NotImplementedException();
	}

	public long GetStreamLastEventNumber_NoCollisions(ulong stream, Func<ulong, TStreamId> getStreamId, long beforePosition) {
		throw new NotImplementedException();
	}
}
