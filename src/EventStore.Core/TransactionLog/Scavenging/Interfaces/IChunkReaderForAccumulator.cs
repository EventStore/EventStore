using System.Collections.Generic;
using EventStore.Core.Helpers;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IChunkReaderForAccumulator<TStreamId> {
		IEnumerable<RecordForAccumulator<TStreamId>> ReadChunk(
			int logicalChunkNumber,
			ReusableObject<RecordForAccumulator<TStreamId>.OriginalStreamRecord> originalStreamRecord,
			ReusableObject<RecordForAccumulator<TStreamId>.MetadataStreamRecord> metadataStreamRecord,
			ReusableObject<RecordForAccumulator<TStreamId>.TombStoneRecord> tombStoneRecord);
	}
}
