using System;
using EventStore.Core.Data;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3 {
	public class StreamIdToNameFromStandardIndex : INameLookup<StreamId> {
		private readonly IIndexReader<StreamId> _indexReader;

		public StreamIdToNameFromStandardIndex(IIndexReader<StreamId> indexReader) {
			_indexReader = indexReader;
		}

		public bool TryGetName(StreamId streamId, out string name) {
			if (streamId % 2 == 1)
				throw new ArgumentOutOfRangeException(nameof(streamId), "streamId must be even");

			// we divided by two when calculating the position in the stream, since we dont
			// explicitly create metastreams.
			var record = _indexReader.ReadPrepare(
				streamId: LogV3SystemStreams.StreamsCreatedStreamNumber,
				eventNumber: StreamIdConverter.ToEventNumber(streamId), tracker: ITransactionFileTracker.NoOp); // noop ok: LogV3

			if (record is null) {
				name = null;
				return false;
			}

			if (record is not LogV3StreamRecord streamRecord)
				throw new Exception($"Unexpected log record type: {record}.");

			name = streamRecord.StreamName;
			return true;
		}

		public bool TryGetLastValue(out StreamId lastValue) {
			var lastEventNumber = _indexReader.GetStreamLastEventNumber(LogV3SystemStreams.StreamsCreatedStreamNumber, ITransactionFileTracker.NoOp); // noop ok: LogV3
			var success = ExpectedVersion.NoStream < lastEventNumber && lastEventNumber != EventNumber.DeletedStream;
			lastValue = StreamIdConverter.ToStreamId(lastEventNumber);
			return success;
		}
	}
}
