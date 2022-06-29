using System.Collections.Generic;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IChunkReaderForAccumulator<TStreamId> {
		// Each element in the enumerable indicates which of the three records has been populated for
		// that iteration.
		IEnumerable<AccumulatorRecordType> ReadChunkInto(
			int logicalChunkNumber,
			RecordForAccumulator<TStreamId>.OriginalStreamRecord originalStreamRecord,
			RecordForAccumulator<TStreamId>.MetadataStreamRecord metadataStreamRecord,
			RecordForAccumulator<TStreamId>.TombStoneRecord tombStoneRecord);
	}
}
