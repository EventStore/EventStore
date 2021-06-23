﻿using System;
using System.Security.Claims;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public sealed class ReadIndex<TStreamId> : IDisposable, IReadIndex<TStreamId> {
		public long LastIndexedPosition {
			get { return _indexCommitter.LastIndexedPosition; }
		}

		public IIndexWriter<TStreamId> IndexWriter {
			get { return _indexWriter; }
		}

		public IIndexCommitter<TStreamId> IndexCommitter {
			get { return _indexCommitter; }
		}

		private readonly IIndexReader<TStreamId> _indexReader;
		private readonly IIndexWriter<TStreamId> _indexWriter;
		private readonly IIndexCommitter<TStreamId> _indexCommitter;
		private readonly IAllReader _allReader;
		private readonly IValueLookup<TStreamId> _streamIds;
		private readonly INameLookup<TStreamId> _streamNames;

		public ReadIndex(IPublisher bus,
			ObjectPool<ITransactionFileReader> readerPool,
			ITableIndex<TStreamId> tableIndex,
			INameIndexConfirmer<TStreamId> streamNameIndex,
			IValueLookup<TStreamId> streamIds,
			IStreamNamesProvider<TStreamId> streamNamesProvider,
			TStreamId emptyStreamName,
			IStreamIdConverter<TStreamId> streamIdConverter,
			IValidator<TStreamId> streamIdValidator,
			ISizer<TStreamId> sizer,
			int streamInfoCacheCapacity,
			bool additionalCommitChecks,
			long metastreamMaxCount,
			int hashCollisionReadLimit,
			bool skipIndexScanOnReads,
			IReadOnlyCheckpoint replicationCheckpoint,
			ICheckpoint indexCheckpoint) {
			Ensure.NotNull(bus, "bus");
			Ensure.NotNull(readerPool, "readerPool");
			Ensure.NotNull(tableIndex, "tableIndex");
			Ensure.NotNull(streamIds, nameof(streamIds));
			Ensure.NotNull(streamNamesProvider, nameof(streamNamesProvider));
			Ensure.NotNull(streamIdValidator, nameof(streamIdValidator));
			Ensure.NotNull(sizer, nameof(sizer));
			Ensure.Nonnegative(streamInfoCacheCapacity, "streamInfoCacheCapacity");
			Ensure.Positive(metastreamMaxCount, "metastreamMaxCount");
			Ensure.NotNull(replicationCheckpoint, "replicationCheckpoint");
			Ensure.NotNull(indexCheckpoint, "indexCheckpoint");

			var metastreamMetadata = new StreamMetadata(maxCount: metastreamMaxCount);

			var indexBackend = new IndexBackend<TStreamId>(readerPool, streamInfoCacheCapacity, streamInfoCacheCapacity);

			_indexReader = new IndexReader<TStreamId>(indexBackend, tableIndex, streamNamesProvider, streamIdValidator, metastreamMetadata, hashCollisionReadLimit,
				skipIndexScanOnReads);

			_streamIds = streamIds;
			_streamNames = streamNamesProvider.StreamNames;
			var systemStreams = streamNamesProvider.SystemStreams;

			_indexWriter = new IndexWriter<TStreamId>(indexBackend, _indexReader, _streamIds, _streamNames, systemStreams, emptyStreamName, sizer);
			_indexCommitter = new IndexCommitter<TStreamId>(bus, indexBackend, _indexReader, tableIndex, streamNameIndex, _streamNames, systemStreams, streamIdConverter, indexCheckpoint, additionalCommitChecks);
			_allReader = new AllReader<TStreamId>(indexBackend, _indexCommitter, _streamNames);
		}

		IndexReadEventResult IReadIndex<TStreamId>.ReadEvent(string streamName, TStreamId streamId, long eventNumber) {
			return _indexReader.ReadEvent(streamName, streamId, eventNumber);
		}

		IndexReadStreamResult IReadIndex<TStreamId>.ReadStreamEventsForward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount) {
			return _indexReader.ReadStreamEventsForward(streamName, streamId, fromEventNumber, maxCount);
		}

		IndexReadStreamResult IReadIndex<TStreamId>.ReadStreamEventsBackward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount) {
			return _indexReader.ReadStreamEventsBackward(streamName, streamId, fromEventNumber, maxCount);
		}

		TStreamId IReadIndex<TStreamId>.GetStreamId(string streamName) {
			return _streamIds.LookupValue(streamName);
		}

		string IReadIndex<TStreamId>.GetStreamName(TStreamId streamId) {
			return _streamNames.LookupName(streamId);
		}

		bool IReadIndex<TStreamId>.IsStreamDeleted(TStreamId streamId) {
			return _indexReader.GetStreamLastEventNumber(streamId) == EventNumber.DeletedStream;
		}

		long IReadIndex<TStreamId>.GetStreamLastEventNumber(TStreamId streamId) {
			return _indexReader.GetStreamLastEventNumber(streamId);
		}

		StreamMetadata IReadIndex<TStreamId>.GetStreamMetadata(TStreamId streamId) {
			return _indexReader.GetStreamMetadata(streamId);
		}

		public TStreamId GetEventStreamIdByTransactionId(long transactionId) {
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

		public StorageMessage.EffectiveAcl GetEffectiveAcl(TStreamId streamId) {
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
