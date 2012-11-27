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
using System.Diagnostics;
using System.IO;
using System.Threading;
using EventStore.Common.Utils;
using System.Linq;

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

        private readonly Common.Concurrent.ConcurrentQueue<TFChunk> _chunksQueue = new Common.Concurrent.ConcurrentQueue<TFChunk>();

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
            var chunkNumber = _chunksCount;
            var chunkName = _config.FileNamingStrategy.GetFilenameFor(chunkNumber);
            var chunk = TFChunk.CreateNew(chunkName, _config.ChunkSize, chunkNumber, 0);
            AddChunk(chunk);
            return chunk;
        }

        public TFChunk AddNewChunk(ChunkHeader chunkHeader, int fileSize)
        {
            Ensure.NotNull(chunkHeader, "chunkHeader");
            Ensure.Positive(fileSize, "fileSize");

            if (chunkHeader.ChunkStartNumber != _chunksCount)
            {
                throw new Exception(string.Format("Received request to create a new ongoing chunk {0}-{1}, but current chunks count is {2}.",
                                                  chunkHeader.ChunkStartNumber,
                                                  chunkHeader.ChunkEndNumber,
                                                  _chunksCount));
            }

            var chunkNumber = _chunksCount;
            var chunkName = _config.FileNamingStrategy.GetFilenameFor(chunkNumber);
            var chunk = TFChunk.CreateWithHeader(chunkName, chunkHeader, fileSize);
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

        public TFChunk GetChunkForOrDefault(string path)
        {
            return _chunks != null
                       ? _chunks.FirstOrDefault(c => c != null && c.FileName == path)
                       : null;
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
            TryCacheChunk(newChunk);
            return oldChunk;
        }

        private void TryCacheChunk(TFChunk chunk)
        {
            if (_cachingEnabled)
            {
                if (!chunk.IsReadOnly)
                {
                    CacheUncacheIfNecessary(chunk);
                }
                else
                {
                    _chunksQueue.Enqueue(chunk);
                    EnsureBackgroundWorkerRunning();
                }
            }
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

        public void AddReplicatedChunk(TFChunk replicatedChunk, bool verifyHash)
        {
            Ensure.NotNull(replicatedChunk, "replicatedChunk");
            if (!replicatedChunk.IsReadOnly)
                throw new ArgumentException(string.Format("Passed TFChunk is not completed: {0}.", replicatedChunk.FileName));

            var chunkHeader = replicatedChunk.ChunkHeader;
            var oldFileName = replicatedChunk.FileName;
            var newFileName = _config.FileNamingStrategy.GetFilenameFor(chunkHeader.ChunkStartNumber, chunkHeader.ChunkScavengeVersion);
            
            replicatedChunk.Dispose();
            try
            {
                replicatedChunk.WaitForDestroy(0); // should happen immediately
            }
            catch (TimeoutException exc)
            {
                throw new Exception(string.Format("Replicated chunk '{0}' ({1}-{2}) is used by someone else.",
                                                  replicatedChunk.FileName,
                                                  replicatedChunk.ChunkHeader.ChunkStartNumber,
                                                  replicatedChunk.ChunkHeader.ChunkEndNumber), exc);
            }

            //TODO AN: temporary workaround
            for (int i = chunkHeader.ChunkStartNumber; i <= chunkHeader.ChunkEndNumber; ++i)
            {
                var oldChunk = _chunks[i];
                if (oldChunk != null)
                {
                    oldChunk.MarkForDeletion();
                    oldChunk.WaitForDestroy(500);
                }
            }

            // TODO AN it is possible that chunk with the newFileName already exists, need to work around that
            // TODO AN this could be caused by scavenging... no scavenge -- no cry :(
            File.Move(oldFileName, newFileName);
            var newChunk = TFChunk.FromCompletedFile(newFileName, verifyHash);

            for (int i = chunkHeader.ChunkStartNumber; i <= chunkHeader.ChunkEndNumber; ++i)
            {
                var oldChunk = Interlocked.Exchange(ref _chunks[i], newChunk);
                if (oldChunk != null)
                    oldChunk.MarkForDeletion();
            }
            _chunksCount = newChunk.ChunkHeader.ChunkEndNumber + 1;
            Debug.Assert(_chunks[_chunksCount] == null);

            TryCacheChunk(newChunk);
        }

        public TFChunk CreateTempChunk(ChunkHeader chunkHeader, int fileSize)
        {
            var chunkFileName = _config.FileNamingStrategy.GetTempFilename();
            return TFChunk.CreateWithHeader(chunkFileName, chunkHeader, fileSize);
        }
    }
}