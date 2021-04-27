using System;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.LogV3 {
	public class StreamIdToNameFromStandardIndex : IStreamNameLookup<long> {
		private readonly IIndexReader<long> _indexReader;

		public StreamIdToNameFromStandardIndex(IIndexReader<long> indexReader) {
			_indexReader = indexReader;
		}

		public string LookupName(long streamId) {
			if (streamId % 2 == 1)
				throw new ArgumentOutOfRangeException(nameof(streamId), "streamId must be even");

			// we divided by two when calculating the position in the stream, since we dont
			// explicitly create metastreams.
			var record = _indexReader.ReadPrepare(LogV3SystemStreams.StreamsCreatedStreamNumber, streamId / 2);

			if (record is not LogV3StreamRecord streamRecord)
				throw new Exception($"Unexpected log record type: {record}.");
			return streamRecord.StreamName;
		}
	}
}
