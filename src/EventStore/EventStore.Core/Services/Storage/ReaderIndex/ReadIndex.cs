// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
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

        private readonly IIndexBackend _indexBackend;
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

            var metastreamMetadata = new StreamMetadata(metastreamMaxCount, null, null, null, null);

            _indexBackend = new IndexBackend(readerPool, streamInfoCacheCapacity, streamInfoCacheCapacity);
            _indexReader = new IndexReader(_indexBackend, hasher, tableIndex, metastreamMetadata);
            _indexWriter = new IndexWriter(_indexBackend, _indexReader);
            _indexCommitter = new IndexCommitter(bus, _indexBackend, _indexReader, tableIndex, hasher, additionalCommitChecks);
            _allReader = new AllReader(_indexBackend);
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
