using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IIndexReaderForAccumulator<TStreamId> {
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
