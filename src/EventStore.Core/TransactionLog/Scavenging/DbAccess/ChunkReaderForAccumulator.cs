using System;
using System.Collections.Generic;
using EventStore.Core.Helpers;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class ChunkReaderForAccumulator<TStreamId> : IChunkReaderForAccumulator<TStreamId> {
		private readonly TFChunkManager _manager;
		private readonly IMetastreamLookup<TStreamId> _metaStreamLookup;
		private readonly IStreamIdConverter<TStreamId> _streamIdConverter;
		private readonly ICheckpoint _replicationChk;
		private readonly Func<int, byte[]> _getBuffer;
		private readonly Action _releaseBuffer;
		private readonly ReusableObject<PrepareLogRecordView> _reusablePrepareView;
		private readonly int _chunkSize;

		public ChunkReaderForAccumulator(
			TFChunkManager manager,
			IMetastreamLookup<TStreamId> metastreamLookup,
			IStreamIdConverter<TStreamId> streamIdConverter,
			ICheckpoint replicationChk,
			int chunkSize) {
			_manager = manager;
			_metaStreamLookup = metastreamLookup;
			_streamIdConverter = streamIdConverter;
			_replicationChk = replicationChk;
			_chunkSize = chunkSize;

			var reusableRecordBuffer = new ReusableBuffer(8192);

			_getBuffer = size => reusableRecordBuffer.AcquireAsByteArray(size);
			_releaseBuffer = () => reusableRecordBuffer.Release();
			_reusablePrepareView = ReusableObject.Create(new PrepareLogRecordView());
		}

		public IEnumerable<RecordForAccumulator<TStreamId>> ReadChunk(
			int logicalChunkNumber,
			ReusableObject<RecordForAccumulator<TStreamId>.OriginalStreamRecord> originalStreamRecord,
			ReusableObject<RecordForAccumulator<TStreamId>.MetadataStreamRecord> metadataStreamRecord,
			ReusableObject<RecordForAccumulator<TStreamId>.TombStoneRecord> tombStoneRecord) {

			// the physical chunk might contain several logical chunks, we are only interested in one of them
			var chunk = _manager.GetChunk(logicalChunkNumber);
			long chunkStartPos = (long)_chunkSize * logicalChunkNumber;
			long chunkEndPos = (long)_chunkSize * (logicalChunkNumber + 1);
			long nextPos = chunkStartPos;

			var replicationChk = _replicationChk.ReadNonFlushed();

			while (true) {
				if (nextPos >= chunkEndPos) // reached the end of this logical chunk
					break;

				//qq review: i think, in v5 the replication checkpoint points to the beginning of the
				// last replicated record, but in v21 it points to the beginning of the _next_ record
				// may need to adjust this inequality in the forward port.
				if (nextPos > replicationChk)
					throw new InvalidOperationException(
						$"Attempt to read at position: {nextPos} which is after the " +
						$"replication checkpoint: {replicationChk}.");

				var localPos = chunk.ChunkHeader.GetLocalLogPosition(nextPos);
				var result = chunk.TryReadClosestForwardRaw(localPos, _getBuffer);

				if (!result.Success)
					break; //qq review: or should this be an exception

				switch (result.RecordType) {
					case LogRecordType.Prepare:
						var prepareViewInitParams = new PrepareLogRecordViewInitParams(result.Record, result.Length, _reusablePrepareView.Release);
						var prepareView = _reusablePrepareView.Acquire(prepareViewInitParams);

						var streamId = _streamIdConverter.ToStreamId(prepareView.EventStreamId);
						var recordInitParams = new RecordForAccumulatorInitParams<TStreamId>(prepareView, streamId);

						if (prepareView.Flags.HasAnyOf(PrepareFlags.StreamDelete)) {
							yield return tombStoneRecord.Acquire(recordInitParams);
						} else if (_metaStreamLookup.IsMetaStream(streamId)) {
							yield return metadataStreamRecord.Acquire(recordInitParams);
						} else {
							yield return originalStreamRecord.Acquire(recordInitParams);
						}
						break;
					case LogRecordType.Commit:
						break;
					case LogRecordType.System:
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(result.RecordType),
							$"Unexpected log record type: {result.RecordType}");
				}

				nextPos = chunk.ChunkHeader.GetGlobalLogPosition(result.NextPosition);
				_releaseBuffer();
			}
		}
	}
}
