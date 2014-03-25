using System;
using System.Security.Principal;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public class ReadIndex : IDisposable, IReadIndex
    {
        public long LastCommitPosition { get { return _indexCommitter.LastCommitPosition; } }
        public IIndexWriter IndexWriter { get { return _indexWriter; } }
        public IIndexCommitter IndexCommitter { get { return _indexCommitter; } }

        private readonly IIndexCache _indexCache;
        private readonly IIndexReader _indexReader;
        private readonly IIndexWriter _indexWriter;
        private readonly IIndexCommitter _indexCommitter;
        private readonly IAllReader _allReader;

        public ReadIndex(IPublisher bus,
                         ObjectPool<ITransactionFileReader> readerPool,
                         ITableIndex tableIndex,
                         IHasher hasher,
                         int streamInfoCacheCapacity,
                         bool additionalCommitChecks,
                         int metastreamMaxCount)
        {
            Ensure.NotNull(bus, "bus");
            Ensure.NotNull(readerPool, "readerPool");
            Ensure.NotNull(tableIndex, "tableIndex");
            Ensure.NotNull(hasher, "hasher");
            Ensure.Nonnegative(streamInfoCacheCapacity, "streamInfoCacheCapacity");
            Ensure.Positive(metastreamMaxCount, "metastreamMaxCount");

            var metastreamMetadata = new StreamMetadata(maxCount: metastreamMaxCount);

            _indexCache = new IndexCache(readerPool, streamInfoCacheCapacity, streamInfoCacheCapacity);
            _indexReader = new IndexReader(_indexCache, hasher, tableIndex, metastreamMetadata);
            var writer = new IndexWriter(bus, tableIndex, hasher, _indexCache, _indexReader, additionalCommitChecks);
            _indexWriter = writer;
            _indexCommitter = writer;
            _allReader = new AllReader(_indexCache);
        }

        void IReadIndex.Init(long buildToPosition)
        {
            _indexCommitter.Init(buildToPosition);
        }

        IndexReadEventResult IReadIndex.ReadEvent(string streamId, int eventNumber)
        {
            return _indexReader.ReadEvent(streamId, eventNumber);
        }

        IndexReadStreamResult IReadIndex.ReadStreamEventsForward(string streamId, int fromEventNumber, int maxCount)
        {
            return _indexReader.ReadStreamEventsForward(streamId, fromEventNumber, maxCount);
        }

        IndexReadStreamResult IReadIndex.ReadStreamEventsBackward(string streamId, int fromEventNumber, int maxCount)
        {
            return _indexReader.ReadStreamEventsBackward(streamId, fromEventNumber, maxCount);
        }

        bool IReadIndex.IsStreamDeleted(string streamId)
        {
            return _indexReader.GetStreamLastEventNumber(streamId) == EventNumber.DeletedStream;
        }

        int IReadIndex.GetStreamLastEventNumber(string streamId)
        {
            return _indexReader.GetStreamLastEventNumber(streamId);
        }

        StreamMetadata IReadIndex.GetStreamMetadata(string streamId)
        {
            return _indexReader.GetStreamMetadata(streamId);
        }

        public string GetEventStreamIdByTransactionId(long transactionId)
        {
            return _indexReader.GetEventStreamIdByTransactionId(transactionId);
        }

        StreamAccess IReadIndex.CheckStreamAccess(string streamId, StreamAccessType streamAccessType, IPrincipal user)
        {
            return _indexReader.CheckStreamAccess(streamId, streamAccessType, user);
        }

        IndexReadAllResult IReadIndex.ReadAllEventsForward(TFPos pos, int maxCount)
        {
            return _allReader.ReadAllEventsForward(pos, maxCount);
        }

        IndexReadAllResult IReadIndex.ReadAllEventsBackward(TFPos pos, int maxCount)
        {
            return _allReader.ReadAllEventsBackward(pos, maxCount);
        }

        ReadIndexStats IReadIndex.GetStatistics()
        {
            return new ReadIndexStats(Interlocked.Read(ref TFChunkReader.CachedReads),
                                      Interlocked.Read(ref TFChunkReader.NotCachedReads),
                                      _indexReader.CachedStreamInfo,
                                      _indexReader.NotCachedStreamInfo,
                                      _indexReader.HashCollisions,
                                      _indexWriter.CachedTransInfo,
                                      _indexWriter.NotCachedTransInfo);
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            _indexCommitter.Dispose();
        }
    }
}
