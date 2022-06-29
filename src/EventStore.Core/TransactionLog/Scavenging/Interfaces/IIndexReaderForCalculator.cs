using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IIndexReaderForCalculator<TStreamId> {
		long GetLastEventNumber(StreamHandle<TStreamId> streamHandle, ScavengePoint scavengePoint);

		IndexReadEventInfoResult ReadEventInfoForward(
			StreamHandle<TStreamId> stream,
			long fromEventNumber,
			int maxCount,
			ScavengePoint scavengePoint);
	}
}
