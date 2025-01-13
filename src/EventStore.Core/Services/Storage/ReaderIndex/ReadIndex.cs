// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Messages;
using EventStore.Core.Metrics;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using static EventStore.Common.Configuration.MetricsConfiguration;

namespace EventStore.Core.Services.Storage.ReaderIndex;

public sealed class ReadIndex<TStreamId> : IDisposable, IReadIndex<TStreamId> {
	public long LastIndexedPosition => IndexCommitter.LastIndexedPosition;

	public IIndexWriter<TStreamId> IndexWriter { get; }
	public IIndexReader<TStreamId> IndexReader { get; }

	public IIndexCommitter<TStreamId> IndexCommitter { get; }

	private readonly AllReader<TStreamId> _allReader;
	private readonly IValueLookup<TStreamId> _streamIds;
	private readonly INameLookup<TStreamId> _streamNames;

	public ReadIndex(IPublisher bus,
		ObjectPool<ITransactionFileReader> readerPool,
		ITableIndex<TStreamId> tableIndex,
		INameIndexConfirmer<TStreamId> streamNameIndex,
		IValueLookup<TStreamId> streamIds,
		IStreamNamesProvider<TStreamId> streamNamesProvider,
		TStreamId emptyStreamName,
		IValidator<TStreamId> streamIdValidator,
		ISizer<TStreamId> sizer,
		INameExistenceFilter streamExistenceFilter,
		IExistenceFilterReader<TStreamId> streamExistenceFilterReader,
		INameIndexConfirmer<TStreamId> eventTypeIndex,
		ILRUCache<TStreamId, IndexBackend<TStreamId>.EventNumberCached> streamLastEventNumberCache,
		ILRUCache<TStreamId, IndexBackend<TStreamId>.MetadataCached> streamMetadataCache,
		bool additionalCommitChecks,
		long metastreamMaxCount,
		int hashCollisionReadLimit,
		bool skipIndexScanOnReads,
		IReadOnlyCheckpoint replicationCheckpoint,
		ICheckpoint indexCheckpoint,
		IIndexStatusTracker indexStatusTracker,
		IIndexTracker indexTracker,
		ICacheHitsMissesTracker cacheTracker) {

		Ensure.NotNull(bus);
		Ensure.NotNull(readerPool);
		Ensure.NotNull(tableIndex);
		Ensure.NotNull(streamIds);
		Ensure.NotNull(streamNamesProvider);
		Ensure.NotNull(streamIdValidator);
		Ensure.NotNull(sizer);
		Ensure.NotNull(streamExistenceFilter);
		Ensure.NotNull(streamExistenceFilterReader);
		Ensure.NotNull(streamLastEventNumberCache);
		Ensure.NotNull(streamMetadataCache);
		Ensure.Positive(metastreamMaxCount, "metastreamMaxCount");
		Ensure.NotNull(replicationCheckpoint);
		Ensure.NotNull(indexCheckpoint);

		var metastreamMetadata = new StreamMetadata(maxCount: metastreamMaxCount);

		var indexBackend = new IndexBackend<TStreamId>(readerPool, streamLastEventNumberCache, streamMetadataCache);

		IndexReader = new IndexReader<TStreamId>(indexBackend, tableIndex, streamNamesProvider, streamIdValidator,
			streamExistenceFilterReader, metastreamMetadata, hashCollisionReadLimit, skipIndexScanOnReads);

		_streamIds = streamIds;
		_streamNames = streamNamesProvider.StreamNames;
		var systemStreams = streamNamesProvider.SystemStreams;
		var eventTypeNames = streamNamesProvider.EventTypes;
		var streamExistenceFilterInitializer = streamNamesProvider.StreamExistenceFilterInitializer;

		IndexWriter = new IndexWriter<TStreamId>(indexBackend, IndexReader, _streamIds, _streamNames, systemStreams, emptyStreamName, sizer);
		IndexCommitter = new IndexCommitter<TStreamId>(bus, indexBackend, IndexReader, tableIndex, streamNameIndex,
			_streamNames, eventTypeIndex, eventTypeNames, systemStreams, streamExistenceFilter,
			streamExistenceFilterInitializer, indexCheckpoint, indexStatusTracker, indexTracker, additionalCommitChecks);
		_allReader = new AllReader<TStreamId>(indexBackend, IndexCommitter, _streamNames, eventTypeNames);

		RegisterHitsMisses(cacheTracker);
	}

	ValueTask<IndexReadEventResult> IReadIndex<TStreamId>.ReadEvent(string streamName, TStreamId streamId, long eventNumber, CancellationToken token) {
		return IndexReader.ReadEvent(streamName, streamId, eventNumber, token);
	}

	ValueTask<IndexReadStreamResult> IReadIndex<TStreamId>.ReadStreamEventsForward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount, CancellationToken token) {
		return IndexReader.ReadStreamEventsForward(streamName, streamId, fromEventNumber, maxCount, token);
	}

	ValueTask<IndexReadStreamResult> IReadIndex<TStreamId>.ReadStreamEventsBackward(string streamName, TStreamId streamId, long fromEventNumber, int maxCount, CancellationToken token) {
		return IndexReader.ReadStreamEventsBackward(streamName, streamId, fromEventNumber, maxCount, token);
	}

	TStreamId IReadIndex<TStreamId>.GetStreamId(string streamName) {
		return _streamIds.LookupValue(streamName);
	}

	public ValueTask<IndexReadEventInfoResult> ReadEventInfo_KeepDuplicates(TStreamId streamId, long eventNumber, CancellationToken token) {
		return IndexReader.ReadEventInfo_KeepDuplicates(streamId, eventNumber, token);
	}

	public ValueTask<IndexReadEventInfoResult> ReadEventInfoForward_KnownCollisions(TStreamId streamId, long fromEventNumber, int maxCount, long beforePosition, CancellationToken token) {
		return IndexReader.ReadEventInfoForward_KnownCollisions(streamId, fromEventNumber, maxCount, beforePosition, token);
	}

	public ValueTask<IndexReadEventInfoResult> ReadEventInfoForward_NoCollisions(ulong stream, long fromEventNumber, int maxCount, long beforePosition, CancellationToken token) {
		return IndexReader.ReadEventInfoForward_NoCollisions(stream, fromEventNumber, maxCount, beforePosition, token);
	}

	public ValueTask<IndexReadEventInfoResult> ReadEventInfoBackward_KnownCollisions(TStreamId streamId, long fromEventNumber, int maxCount,
		long beforePosition, CancellationToken token) {
		return IndexReader.ReadEventInfoBackward_KnownCollisions(streamId, fromEventNumber, maxCount, beforePosition, token);
	}

	public ValueTask<IndexReadEventInfoResult> ReadEventInfoBackward_NoCollisions(ulong stream, Func<ulong, TStreamId> getStreamId,
		long fromEventNumber, int maxCount, long beforePosition, CancellationToken token) {
		return IndexReader.ReadEventInfoBackward_NoCollisions(stream, getStreamId, fromEventNumber, maxCount, beforePosition, token);
	}

	ValueTask<string> IReadIndex<TStreamId>.GetStreamName(TStreamId streamId, CancellationToken token) {
		return _streamNames.LookupName(streamId, token);
	}

	async ValueTask<bool> IReadIndex<TStreamId>.IsStreamDeleted(TStreamId streamId, CancellationToken token) {
		return await IndexReader.GetStreamLastEventNumber(streamId, token) is EventNumber.DeletedStream;
	}

	ValueTask<long> IReadIndex<TStreamId>.GetStreamLastEventNumber(TStreamId streamId, CancellationToken token) {
		return IndexReader.GetStreamLastEventNumber(streamId, token);
	}

	public ValueTask<long> GetStreamLastEventNumber_KnownCollisions(TStreamId streamId, long beforePosition, CancellationToken token) {
		return IndexReader.GetStreamLastEventNumber_KnownCollisions(streamId, beforePosition, token);
	}

	public ValueTask<long> GetStreamLastEventNumber_NoCollisions(ulong stream, Func<ulong, TStreamId> getStreamId, long beforePosition, CancellationToken token) {
		return IndexReader.GetStreamLastEventNumber_NoCollisions(stream, getStreamId, beforePosition, token);
	}

	ValueTask<StreamMetadata> IReadIndex<TStreamId>.GetStreamMetadata(TStreamId streamId, CancellationToken token) {
		return IndexReader.GetStreamMetadata(streamId, token);
	}

	public ValueTask<TStreamId> GetEventStreamIdByTransactionId(long transactionId, CancellationToken token) {
		return IndexReader.GetEventStreamIdByTransactionId(transactionId, token);
	}

	ValueTask<IndexReadAllResult> IReadIndex.ReadAllEventsForward(TFPos pos, int maxCount, CancellationToken token) {
		return _allReader.ReadAllEventsForward(pos, maxCount, token);
	}

	ValueTask<IndexReadAllResult> IReadIndex.ReadAllEventsForwardFiltered(TFPos pos, int maxCount, int maxSearchWindow,
		IEventFilter eventFilter, CancellationToken token) {
		return _allReader.FilteredReadAllEventsForward(pos, maxCount, maxSearchWindow, eventFilter, token);
	}

	ValueTask<IndexReadAllResult> IReadIndex.ReadAllEventsBackwardFiltered(TFPos pos, int maxCount, int maxSearchWindow,
		IEventFilter eventFilter, CancellationToken token) {
		return _allReader.FilteredReadAllEventsBackward(pos, maxCount, maxSearchWindow, eventFilter, token);
	}

	ValueTask<IndexReadAllResult> IReadIndex.ReadAllEventsBackward(TFPos pos, int maxCount, CancellationToken token) {
		return _allReader.ReadAllEventsBackward(pos, maxCount, token);
	}

	public ValueTask<StorageMessage.EffectiveAcl> GetEffectiveAcl(TStreamId streamId, CancellationToken token) {
		return IndexReader.GetEffectiveAcl(streamId, token);
	}

	void RegisterHitsMisses(ICacheHitsMissesTracker tracker) {
		tracker.Register(
			Cache.Chunk,
			() => Interlocked.Read(ref TFChunkReader.CachedReads),
			() => Interlocked.Read(ref TFChunkReader.NotCachedReads));

		tracker.Register(
			Cache.StreamInfo,
			() => IndexReader.CachedStreamInfo,
			() => IndexReader.NotCachedStreamInfo);
	}

	ReadIndexStats IReadIndex.GetStatistics() {
		return new ReadIndexStats(Interlocked.Read(ref TFChunkReader.CachedReads),
			Interlocked.Read(ref TFChunkReader.NotCachedReads),
			IndexReader.CachedStreamInfo,
			IndexReader.NotCachedStreamInfo,
			IndexReader.HashCollisions,
			IndexWriter.CachedTransInfo,
			IndexWriter.NotCachedTransInfo);
	}

	public void Close() {
		Dispose();
	}

	public void Dispose() {
		IndexCommitter.Dispose();
	}
}
