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
using System.Threading;
using EventStore.Common.Utils;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkManager: IDisposable
    {
        public const int MaxChunksCount = 100000; // that's enough for about 25 Tb of data

        public int ChunksCount { get { return _chunksCount; } }

        private readonly TFChunkDbConfig _config;
        private readonly TFChunk[] _chunks = new TFChunk[MaxChunksCount]; 
        private volatile int _chunksCount;
        private volatile bool _cachingEnabled;

        private readonly object _backgroundLock = new object();
        private bool _backgroundRunning;

#if __MonoCS__
        private readonly Common.ConcurrentCollections.ConcurrentQueue<TFChunk> _chunksQueue = new Common.ConcurrentCollections.ConcurrentQueue<TFChunk>();
#else
        private readonly System.Collections.Concurrent.ConcurrentQueue<TFChunk> _chunksQueue = new System.Collections.Concurrent.ConcurrentQueue<TFChunk>();
#endif

        public TFChunkManager(TFChunkDbConfig config)
        {
            Ensure.NotNull(config, "config");
            _config = config;
        }

        public void EnableCaching()
        {
            _cachingEnabled = true;
            for (int i = 0; i < _chunksCount; ++i)
            {
                var chunk = _chunks[i];
                if (!chunk.IsReadOnly)
                    CacheUncacheIfNecessary(chunk);
                else
                    _chunksQueue.Enqueue(_chunks[i]);
            }
            EnsureBackgroundWorkerRunning();
        }

        public void DisableCaching()
        {
            _cachingEnabled = false;
            for (int i = 0; i < _chunksCount; ++i)
            {
                _chunksQueue.Enqueue(_chunks[i]);
            }
            EnsureBackgroundWorkerRunning();
        }

        private void EnsureBackgroundWorkerRunning()
        {
            lock (_backgroundLock)
            {
                if (_backgroundRunning)
                    return;
                ThreadPool.QueueUserWorkItem(_ => BackgroundProcessing());
            }
        }

        private void BackgroundProcessing()
        {
            while (true)
            {
                lock (_backgroundLock)
                {
                    if (_chunksQueue.Count == 0)
                    {
                        _backgroundRunning = false;
                        return;
                    }
                }
                TFChunk chunk;
                while (_chunksQueue.TryDequeue(out chunk))
                {
                    CacheUncacheIfNecessary(chunk);
                }
            }
        }

        private void CacheUncacheIfNecessary(TFChunk chunk)
        {
            var chunkNumber = chunk.ChunkHeader.ChunkStartNumber;
            if (_cachingEnabled
                && _chunksCount - chunkNumber <= _config.CachedChunkCount
                && ReferenceEquals(chunk, _chunks[chunkNumber]))
            {
                chunk.CacheInMemory();
            }
            else
            {
                chunk.UnCacheFromMemory();
            }
        }

        public TFChunk AddNewChunk()
        {
            var chunksCnt = _chunksCount;
            var chunkName = _config.FileNamingStrategy.GetFilenameFor(chunksCnt);
            var chunk = TFChunk.CreateNew(chunkName, _config.ChunkSize, chunksCnt, 0);
            AddChunk(chunk);
            return chunk;
        }

        public void AddChunk(TFChunk chunk)
        {
            Ensure.NotNull(chunk, "chunk");

            _chunks[_chunksCount] = chunk;
            _chunksCount += 1;

            if (_cachingEnabled)
            {
                int uncacheIndex = _chunksCount - _config.CachedChunkCount - 1;
                if (uncacheIndex >= 0)
                {
                    _chunksQueue.Enqueue(_chunks[uncacheIndex]);
                    EnsureBackgroundWorkerRunning();
                }

                if (_cachingEnabled)
                {
                    if (!chunk.IsReadOnly)
                        CacheUncacheIfNecessary(chunk);
                    else
                    {
                        _chunksQueue.Enqueue(chunk);
                        EnsureBackgroundWorkerRunning();
                    }
                }
            }
        }

        public TFChunk GetChunkFor(long logPosition)
        {
            return GetChunk((int)(logPosition / _config.ChunkSize));
        }

        public TFChunk GetChunk(int chunkNumber)
        {
            Ensure.Nonnegative(chunkNumber, "chunkNumber");
            if (chunkNumber >= MaxChunksCount) 
                throw new ArgumentOutOfRangeException("chunkNumber");

            var chunk = _chunks[chunkNumber];
//            if (chunk == null)
//                throw new InvalidOperationException(
//                        string.Format("Requested chunk #{0}, which is not present in TFChunkManager.", chunkNumber));
            return chunk;
        }

        public TFChunk SwapChunk(int chunkNumber, TFChunk newChunk)
        {
            var oldChunk = Interlocked.Exchange(ref _chunks[chunkNumber], newChunk);
            oldChunk.UnCacheFromMemory();

            if (_cachingEnabled)
            {
                if (!newChunk.IsReadOnly)
                    CacheUncacheIfNecessary(newChunk);
                else
                {
                    _chunksQueue.Enqueue(newChunk);
                    EnsureBackgroundWorkerRunning();
                }
            }

            return oldChunk;
        }

        public void Dispose()
        {
            // NOT THREAD-SAFE
            for (int i=0; i<_chunksCount; ++i)
            {
                if (_chunks[i] != null)
                    _chunks[i].Dispose();
            }
        }
    }
}