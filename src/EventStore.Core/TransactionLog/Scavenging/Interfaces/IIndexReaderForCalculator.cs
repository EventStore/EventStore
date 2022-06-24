using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IIndexReaderForCalculator<TStreamId> {
		//qq review: consider whether to have maxposition / positionlimit instead of scavengepoint?
		//qq review: presumably this returns NoStream if the only events in the stream are beyond the scavenge point
		long GetLastEventNumber(StreamHandle<TStreamId> streamHandle, ScavengePoint scavengePoint);

		//qq review: needs to return index entries even for deleted streams, and regardless of the metadata
		// applied to the stream
		IndexReadEventInfoResult ReadEventInfoForward(
			StreamHandle<TStreamId> stream,
			long fromEventNumber,
			int maxCount,
			ScavengePoint scavengePoint);
	}
}
