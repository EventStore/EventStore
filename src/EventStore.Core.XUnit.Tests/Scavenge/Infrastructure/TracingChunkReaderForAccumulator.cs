using System;
using System.Collections.Generic;
using EventStore.Core.Helpers;
using EventStore.Core.TransactionLog.Scavenging;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	public class TracingChunkReaderForAccumulator<TStreamId> : IChunkReaderForAccumulator<TStreamId> {
		private readonly IChunkReaderForAccumulator<TStreamId> _wrapped;
		private readonly Action<string> _trace;

		public TracingChunkReaderForAccumulator(
			IChunkReaderForAccumulator<TStreamId> wrapped,
			Action<string> trace) {

			_wrapped = wrapped;
			_trace = trace;
		}

		public IEnumerable<RecordForAccumulator<TStreamId>> ReadChunk(
			int logicalChunkNumber,
			ReusableObject<RecordForAccumulator<TStreamId>.OriginalStreamRecord> originalStreamRecord,
			ReusableObject<RecordForAccumulator<TStreamId>.MetadataStreamRecord> metadataStreamRecord,
			ReusableObject<RecordForAccumulator<TStreamId>.TombStoneRecord> tombStoneRecord) {

			var ret = _wrapped.ReadChunk(
				logicalChunkNumber,
				originalStreamRecord,
				metadataStreamRecord,
				tombStoneRecord);

			_trace($"Reading Chunk {logicalChunkNumber}");
			return ret;
		}
	}
}
