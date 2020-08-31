using System;
using System.Security.Claims;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Index;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLogV2;
using EventStore.Core.TransactionLogV2.Checkpoint;
using EventStore.Core.TransactionLogV2.Chunks;
using EventStore.Core.TransactionLogV2.Data;
using EventStore.Core.TransactionLogV2.DataStructures;
using EventStore.Core.Util;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public sealed class ReadIndex : IDisposable, IReadIndex {
		public long LastIndexedPosition {
			get { return _indexCommitter.LastIndexedPosition; }
		}

		public IIndexWriter IndexWriter {
			get { return _indexWriter; }
		}

		public IIndexCommitter IndexCommitter {
			get { return _indexCommitter; }
		}

		private readonly IIndexReader _indexReader;
		private readonly IIndexWriter _indexWriter;
		private readonly IIndexCommitter _indexCommitter;
		private readonly IAllReader _allReader;

		public ReadIndex(IPublisher bus,
			ObjectPool<ITransactionFileReader> readerPool,
			ITableIndex tableIndex,
			int streamInfoCacheCapacity,
			bool additionalCommitChecks,
			long metastreamMaxCount,
			int hashCollisionReadLimit,
			bool skipIndexScanOnReads,
			ICheckpoint replicationCheckpoint,
			ICheckpoint indexCheckpoint) {
			Ensure.NotNull(bus, "bus");
			Ensure.NotNull(readerPool, "readerPool");
			Ensure.NotNull(tableIndex, "tableIndex");
			Ensure.Nonnegative(streamInfoCacheCapacity, "streamInfoCacheCapacity");
			Ensure.Positive(metastreamMaxCount, "metastreamMaxCount");
			Ensure.NotNull(replicationCheckpoint, "replicationCheckpoint");
			Ensure.NotNull(indexCheckpoint, "indexCheckpoint");

			var metastreamMetadata = new StreamMetadata(maxCount: metastreamMaxCount);

			IIndexBackend indexBackend = new IndexBackend(readerPool, streamInfoCacheCapacity, streamInfoCacheCapacity);
			_indexReader = new IndexReader(indexBackend, tableIndex, metastreamMetadata, hashCollisionReadLimit,
				skipIndexScanOnReads);
			_indexWriter = new IndexWriter(indexBackend, _indexReader);
			_indexCommitter = new IndexCommitter(bus, indexBackend, _indexReader, tableIndex, indexCheckpoint,additionalCommitChecks);
			_allReader = new AllReader(indexBackend, _indexCommitter);
		}

		IndexReadEventResult IReadIndex.ReadEvent(string streamId, long eventNumber) {
			return _indexReader.ReadEvent(streamId, eventNumber);
		}

		IndexReadStreamResult IReadIndex.ReadStreamEventsForward(string streamId, long fromEventNumber, int maxCount) {
			return _indexReader.ReadStreamEventsForward(streamId, fromEventNumber, maxCount);
		}

		IndexReadStreamResult IReadIndex.ReadStreamEventsBackward(string streamId, long fromEventNumber, int maxCount) {
			return _indexReader.ReadStreamEventsBackward(streamId, fromEventNumber, maxCount);
		}

		bool IReadIndex.IsStreamDeleted(string streamId) {
			return _indexReader.GetStreamLastEventNumber(streamId) == EventNumber.DeletedStream;
		}

		long IReadIndex.GetStreamLastEventNumber(string streamId) {
			return _indexReader.GetStreamLastEventNumber(streamId);
		}

		StreamMetadata IReadIndex.GetStreamMetadata(string streamId) {
			return _indexReader.GetStreamMetadata(streamId);
		}

		public string GetEventStreamIdByTransactionId(long transactionId) {
			return _indexReader.GetEventStreamIdByTransactionId(transactionId);
		}

		IndexReadAllResult IReadIndex.ReadAllEventsForward(TFPos pos, int maxCount) {
			return _allReader.ReadAllEventsForward(pos, maxCount);
		}

		IndexReadAllResult IReadIndex.ReadAllEventsForwardFiltered(TFPos pos, int maxCount, int maxSearchWindow,
			IEventFilter eventFilter) {
			return _allReader.FilteredReadAllEventsForward(pos, maxCount, maxSearchWindow, eventFilter);
		}

		IndexReadAllResult IReadIndex.ReadAllEventsBackwardFiltered(TFPos pos, int maxCount, int maxSearchWindow,
			IEventFilter eventFilter) {
			return _allReader.FilteredReadAllEventsBackward(pos, maxCount, maxSearchWindow, eventFilter);
		}

		IndexReadAllResult IReadIndex.ReadAllEventsBackward(TFPos pos, int maxCount) {
			return _allReader.ReadAllEventsBackward(pos, maxCount);
		}

		public StorageMessage.EffectiveAcl GetEffectiveAcl(string streamId) {
			return _indexReader.GetEffectiveAcl(streamId);
		}

		ReadIndexStats IReadIndex.GetStatistics() {
			return new ReadIndexStats(Interlocked.Read(ref TFChunkReader.CachedReads),
				Interlocked.Read(ref TFChunkReader.NotCachedReads),
				_indexReader.CachedStreamInfo,
				_indexReader.NotCachedStreamInfo,
				_indexReader.HashCollisions,
				_indexWriter.CachedTransInfo,
				_indexWriter.NotCachedTransInfo);
		}

		public void Close() {
			Dispose();
		}

		public void Dispose() {
			_indexCommitter.Dispose();
		}
	}
}
