using System.Collections.Generic;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class ChunkReaderForExecutor<TStreamId> : IChunkReaderForExecutor<TStreamId, ILogRecord> {
		private readonly TFChunk _chunk;
		private readonly ITransactionFileTracker _tracker;

		public ChunkReaderForExecutor(TFChunk chunk,
			ITransactionFileTracker tracker) {
			_chunk = chunk;
			_tracker = tracker;
		}

		public string Name => _chunk.ToString();

		public int FileSize => _chunk.FileSize;

		public int ChunkStartNumber => _chunk.ChunkHeader.ChunkStartNumber;

		public int ChunkEndNumber => _chunk.ChunkHeader.ChunkEndNumber;

		public bool IsReadOnly => _chunk.IsReadOnly;

		public long ChunkStartPosition => _chunk.ChunkHeader.ChunkStartPosition;

		public long ChunkEndPosition => _chunk.ChunkHeader.ChunkEndPosition;

		// similar to TFChunkScavenger.TraverseChunkBasic
		public IEnumerable<bool> ReadInto(
			RecordForExecutor<TStreamId, ILogRecord>.NonPrepare nonPrepare,
			RecordForExecutor<TStreamId, ILogRecord>.Prepare prepare) {

			var result = _chunk.TryReadFirst(_tracker);
			while (result.Success) {
				var record = result.LogRecord;
				if (record.RecordType != LogRecordType.Prepare) {
					nonPrepare.SetRecord(result.RecordLength, record);
					yield return false;
				} else {
					var sourcePrepare = record as IPrepareLogRecord<TStreamId>;
					prepare.SetRecord(
						length: result.RecordLength,
						logPosition: record.LogPosition,
						record: record,
						timeStamp: sourcePrepare.TimeStamp,
						streamId: sourcePrepare.EventStreamId,
						isSelfCommitted: sourcePrepare.Flags.HasAnyOf(PrepareFlags.IsCommitted),
						isTombstone: sourcePrepare.Flags.HasAnyOf(PrepareFlags.StreamDelete),
						isTransactionBegin: sourcePrepare.Flags.HasAnyOf(PrepareFlags.TransactionBegin),
						eventNumber: sourcePrepare.ExpectedVersion + 1);
					yield return true;
				}

				result = _chunk.TryReadClosestForward(result.NextPosition, _tracker);
			}
		}
	}
}
