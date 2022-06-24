using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IIndexReaderForAccumulator<TStreamId> {
		//qq review: consider whether to have maxposition / positionlimit instead of scavengepoint?
		IndexReadEventInfoResult ReadEventInfoForward(
			StreamHandle<TStreamId> handle,
			long fromEventNumber,
			int maxCount,
			ScavengePoint scavengePoint);

		IndexReadEventInfoResult ReadEventInfoBackward(
			TStreamId streamId,
			StreamHandle<TStreamId> handle,
			long fromEventNumber,
			int maxCount,
			ScavengePoint scavengePoint);
	}
}
