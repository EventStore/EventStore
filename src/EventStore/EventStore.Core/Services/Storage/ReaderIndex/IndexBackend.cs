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

        bool TryGetStreamInfo(string streamId, out StreamCacheInfo streamCacheInfo);

        /// <summary>
        /// Conditional stream cache info update.
        /// Before updating info check that previous stream info version is correct.
        /// If version differs (someone updated already) nothing is changed.
        /// </summary>
        StreamCacheInfo UpdateStreamInfo(int cacheVersion, string streamId, int? lastEventNumber, StreamMetadata streamMetadata);

        /// <summary>
        /// Unconditional stream metadata cache info update.
        /// Should be used only by Commit procedure to let others now to re-read stream metadata.
        /// </summary>
        StreamCacheInfo SetStreamMetadata(string streamId, StreamMetadata metadata);

        /// <summary>
        /// Unconditional stream last event number cache info update.
        /// Should be used only by Commit procedure.
        /// </summary>
        StreamCacheInfo SetStreamLastEventNumber(string streamId, int lastEventNumber);

        void SetSystemSettings(SystemSettings systemSettings);
        SystemSettings GetSystemSettings();
    }
    
    public class IndexBackend : IIndexBackend
    {
        private readonly ObjectPool<ITransactionFileReader> _readers;
        private readonly ILRUCache<string, StreamCacheInfo> _streamInfoCache;
        private SystemSettings _systemSettings;

        public IndexBackend(ObjectPool<ITransactionFileReader> readers,
                            ILRUCache<string, StreamCacheInfo> streamInfoCache)
        {
            Ensure.NotNull(readers, "readers");
            Ensure.NotNull(streamInfoCache, "streamInfoCache");

            _readers = readers;
            _streamInfoCache = streamInfoCache;
        }

        public TFReaderLease BorrowReader()
        {
            return new TFReaderLease(_readers);
        }

        public bool TryGetStreamInfo(string streamId, out StreamCacheInfo streamCacheInfo)
        {
            return _streamInfoCache.TryGet(streamId, out streamCacheInfo);
        }

        public StreamCacheInfo UpdateStreamInfo(int cacheVersion, string streamId,
                                                int? lastEventNumber, StreamMetadata streamMetadata)
        {
            return _streamInfoCache.Put(
                streamId,
                key => cacheVersion == 0
                           ? new StreamCacheInfo(1, lastEventNumber, streamMetadata)
                           : new StreamCacheInfo(1, null, null),
                (key, old) => old.Version == cacheVersion
                                  ? new StreamCacheInfo(old.Version + 1, lastEventNumber ?? old.LastEventNumber, streamMetadata ?? old.Metadata)
                                  : old);
        }

        public StreamCacheInfo SetStreamMetadata(string streamId, StreamMetadata metadata)
        {
            return _streamInfoCache.Put(streamId,
                                        key => new StreamCacheInfo(1, null, metadata),
                                        (key, old) => new StreamCacheInfo(old.Version + 1, old.LastEventNumber, metadata));
        }

        public StreamCacheInfo SetStreamLastEventNumber(string streamId, int lastEventNumber)
        {
            return _streamInfoCache.Put(streamId,
                                        key => new StreamCacheInfo(1, lastEventNumber, null),
                                        (key, old) => new StreamCacheInfo(old.Version + 1, lastEventNumber, old.Metadata));
        }

        public void SetSystemSettings(SystemSettings systemSettings)
        {
            _systemSettings = systemSettings;
        }

        public SystemSettings GetSystemSettings()
        {
            return _systemSettings;
        }
    }
}