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

using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.TransactionLog;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public interface IIndexBackend
    {
        TFReaderLease BorrowReader();

        IndexBackend.EventNumberCached TryGetStreamLastEventNumber(string streamId);
        IndexBackend.MetadataCached TryGetStreamMetadata(string streamId);

        int? UpdateStreamLastEventNumber(int cacheVersion, string streamId, int? lastEventNumber);
        StreamMetadata UpdateStreamMetadata(int cacheVersion, string streamId, StreamMetadata metadata);

        int? SetStreamLastEventNumber(string streamId, int lastEventNumber);
        StreamMetadata SetStreamMetadata(string streamId, StreamMetadata metadata);

        void SetSystemSettings(SystemSettings systemSettings);
        SystemSettings GetSystemSettings();
    }
    
    public class IndexBackend : IIndexBackend
    {
        private readonly ObjectPool<ITransactionFileReader> _readers;
        private readonly ILRUCache<string, EventNumberCached> _streamLastEventNumberCache;
        private readonly ILRUCache<string, MetadataCached> _streamMetadataCache;
        private SystemSettings _systemSettings;

        public IndexBackend(ObjectPool<ITransactionFileReader> readers,
                            int lastEventNumberCacheCapacity,
                            int metadataCacheCapacity)
        {
            Ensure.NotNull(readers, "readers");

            _readers = readers;
            _streamLastEventNumberCache = new LRUCache<string, EventNumberCached>(lastEventNumberCacheCapacity);
            _streamMetadataCache = new LRUCache<string, MetadataCached>(metadataCacheCapacity);
        }

        public TFReaderLease BorrowReader()
        {
            return new TFReaderLease(_readers);
        }

        public EventNumberCached TryGetStreamLastEventNumber(string streamId)
        {
            EventNumberCached cacheInfo;
            _streamLastEventNumberCache.TryGet(streamId, out cacheInfo);
            return cacheInfo;
        }

        public MetadataCached TryGetStreamMetadata(string streamId)
        {
            MetadataCached cacheInfo;
            _streamMetadataCache.TryGet(streamId, out cacheInfo);
            return cacheInfo;
        }

        public int? UpdateStreamLastEventNumber(int cacheVersion, string streamId, int? lastEventNumber)
        {
            var res = _streamLastEventNumberCache.Put(
                streamId,
                key => cacheVersion == 0 ? new EventNumberCached(1, lastEventNumber) : new EventNumberCached(1, null),
                (key, old) => old.Version == cacheVersion ? new EventNumberCached(cacheVersion+1, lastEventNumber ?? old.LastEventNumber) : old);
            return res.LastEventNumber;
        }

        public StreamMetadata UpdateStreamMetadata(int cacheVersion, string streamId, StreamMetadata metadata)
        {
            var res = _streamMetadataCache.Put(
                streamId,
                key => cacheVersion == 0 ? new MetadataCached(1, metadata) : new MetadataCached(1, null),
                (key, old) => old.Version == cacheVersion ? new MetadataCached(cacheVersion + 1, metadata ?? old.Metadata) : old);
            return res.Metadata;
        }

        int? IIndexBackend.SetStreamLastEventNumber(string streamId, int lastEventNumber)
        {
            var res = _streamLastEventNumberCache.Put(streamId,
                                                      key => new EventNumberCached(1, lastEventNumber), 
                                                      (key, old) => new EventNumberCached(old.Version + 1, lastEventNumber));
            return res.LastEventNumber;
        }

        StreamMetadata IIndexBackend.SetStreamMetadata(string streamId, StreamMetadata metadata)
        {
            var res = _streamMetadataCache.Put(streamId,
                                               key => new MetadataCached(1, metadata),
                                               (key, old) => new MetadataCached(old.Version + 1, metadata));
            return res.Metadata;
        }

        public void SetSystemSettings(SystemSettings systemSettings)
        {
            _systemSettings = systemSettings;
        }

        public SystemSettings GetSystemSettings()
        {
            return _systemSettings;
        }

        public struct EventNumberCached
        {
            public readonly int Version;
            public readonly int? LastEventNumber;

            public EventNumberCached(int version, int? lastEventNumber)
            {
                Version = version;
                LastEventNumber = lastEventNumber;
            }
        }

        public struct MetadataCached
        {
            public readonly int Version;
            public readonly StreamMetadata Metadata;

            public MetadataCached(int version, StreamMetadata metadata)
            {
                Version = version;
                Metadata = metadata;
            }
        }
    }
}