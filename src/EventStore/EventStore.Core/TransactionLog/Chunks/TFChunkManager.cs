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
using System.IO;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using System.Linq;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkManager: IDisposable
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<TFChunkManager>();

        public const int MaxChunksCount = 100000; // that's enough for about 25 Tb of data

        public int ChunksCount { get { return _chunksCount; } }
        private readonly TFChunkDbConfig _config;
        private readonly TFChunk.TFChunk[] _chunks = new TFChunk.TFChunk[MaxChunksCount]; 
        
        private volatile int _chunksCount;
        private volatile bool _cachingEnabled;
        private readonly Common.Concurrent.ConcurrentQueue<TFChunk.TFChunk> _chunksQueue = new Common.Concurrent.ConcurrentQueue<TFChunk.TFChunk>();
        private int _backgroundRunning;

        public TFChunkManager(TFChunkDbConfig config)
        {
            Ensure.NotNull(config, "config");
            _config = config;
        }

        public void EnableCaching()
        {
            _cachingEnabled = true;

            int chunkNum = 0;
            while (chunkNum < _chunksCount)
            {
                var chunk = _chunks[chunkNum];
                if (!chunk.IsReadOnly)
                    CacheUncacheForeground(chunk);
                else
                    CacheUncacheInBackground(chunk);
                chunkNum = chunk.ChunkHeader.ChunkEndNumber + 1;
            }
        }

        public void DisableCaching()
        {
            _cachingEnabled = false;
            int chunkNum = 0;
            while (chunkNum < _chunksCount)
            {
                var chunk = _chunks[chunkNum];
                CacheUncacheInBackground(chunk);
                chunkNum = chunk.ChunkHeader.ChunkEndNumber + 1;
            }
        }

        private void CacheUncacheInBackground(TFChunk.TFChunk chunk)
        {
            _chunksQueue.Enqueue(chunk);
            if (Interlocked.CompareExchange(ref _backgroundRunning, 1, 0) == 0)
                ThreadPool.QueueUserWorkItem(BackgroundProcessing);
        }

        private void BackgroundProcessing(object state)
        {
            do
            {
                TFChunk.TFChunk chunk;
                while (_chunksQueue.TryDequeue(out chunk))
                {
                    CacheUncacheForeground(chunk);
                }
                Interlocked.Exchange(ref _backgroundRunning, 0);
            } while (_chunksQueue.Count > 0 && Interlocked.CompareExchange(ref _backgroundRunning, 1, 0) == 0);
        }

        private void CacheUncacheForeground(TFChunk.TFChunk chunk)
        {
            var chunkNumber = chunk.ChunkHeader.ChunkStartNumber;
            var toCache = _cachingEnabled
                          && _chunksCount - chunkNumber <= _config.CachedChunkCount
                          && ReferenceEquals(chunk, _chunks[chunkNumber]);
            if (toCache)
                chunk.CacheInMemory();
            else
                chunk.UnCacheFromMemory();
        }

        public TFChunk.TFChunk AddNewChunk()
        {
            var chunkNumber = _chunksCount;
            var chunkName = _config.FileNamingStrategy.GetFilenameFor(chunkNumber, 0);
            var chunk = TFChunk.TFChunk.CreateNew(chunkName, _config.ChunkSize, chunkNumber, chunkNumber, isScavenged: false);
            AddChunk(chunk);
            return chunk;
        }

        public TFChunk.TFChunk AddNewChunk(ChunkHeader chunkHeader, int fileSize)
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

            var chunkName = _config.FileNamingStrategy.GetFilenameFor(chunkHeader.ChunkStartNumber, 0);
            var chunk = TFChunk.TFChunk.CreateWithHeader(chunkName, chunkHeader, fileSize);
            AddChunk(chunk);
            return chunk;
        }   

        public void AddChunk(TFChunk.TFChunk chunk)
        {
            Ensure.NotNull(chunk, "chunk");

            for (int i = chunk.ChunkHeader.ChunkStartNumber; i <= chunk.ChunkHeader.ChunkEndNumber; ++i)
            {
                _chunks[i] = chunk;
            }
            _chunksCount = chunk.ChunkHeader.ChunkEndNumber + 1;

            if (_cachingEnabled)
            {
                int uncacheIndex = _chunksCount - _config.CachedChunkCount - 1;
                if (uncacheIndex >= 0)
                    CacheUncacheInBackground(_chunks[uncacheIndex]);

                if (_cachingEnabled)
                {
                    if (!chunk.IsReadOnly)
                        CacheUncacheForeground(chunk);
                    else
                        CacheUncacheInBackground(chunk);
                }
            }
        }

        public TFChunk.TFChunk GetChunkFor(long logPosition)
        {
            var chunkNum = (int)(logPosition / _config.ChunkSize);
            if (chunkNum < 0 || chunkNum >= ChunksCount)
                throw new ArgumentOutOfRangeException("logPosition", string.Format("LogPosition {0} doesn't have corresponding chunk in DB.", logPosition));

            var chunk = _chunks[chunkNum];
            if (chunk == null)
                throw new Exception(string.Format("Requested chunk for LogPosition {0}, which is not present in TFChunkManager.", logPosition));
            return chunk;
        }

        public TFChunk.TFChunk GetChunk(int chunkNum)
        {
            if (chunkNum < 0 || chunkNum >= ChunksCount)
                throw new ArgumentOutOfRangeException("chunkNum", string.Format("Chunk #{0} isn't present in DB.", chunkNum));

            var chunk = _chunks[chunkNum];
            if (chunk == null)
                throw new Exception(string.Format("Requested chunk #{0}, which is not present in TFChunkManager.", chunkNum));
            return chunk;
        }

        public TFChunk.TFChunk GetChunkForOrDefault(string path)
        {
            return _chunks != null ? _chunks.FirstOrDefault(c => c != null && c.FileName == path) : null;
        }

        private void TryCacheChunk(TFChunk.TFChunk chunk)
        {
            if (_cachingEnabled)
            {
                if (chunk.IsReadOnly)
                    CacheUncacheInBackground(chunk);
                else
                    CacheUncacheForeground(chunk);
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

        public TFChunk.TFChunk SwitchChunk(TFChunk.TFChunk chunk, bool verifyHash, bool replaceChunksWithGreaterNumbers)
        {
            Ensure.NotNull(chunk, "chunk");
            if (!chunk.IsReadOnly)
                throw new ArgumentException(string.Format("Passed TFChunk is not completed: {0}.", chunk.FileName));

            var chunkHeader = chunk.ChunkHeader;
            var oldFileName = chunk.FileName;

            Log.Info("Switching chunk #{0}-{1} ({2})...", chunkHeader.ChunkStartNumber, chunkHeader.ChunkEndNumber, oldFileName);

            chunk.Dispose();
            try
            {
                chunk.WaitForDestroy(0); // should happen immediately
            }
            catch (TimeoutException exc)
            {
                throw new Exception(string.Format("The chunk that is being switched #{0}-{1} ({2}) is used by someone else.",
                                                  chunk.ChunkHeader.ChunkStartNumber,
                                                  chunk.ChunkHeader.ChunkEndNumber,
                                                  chunk.FileName), 
                                    exc);
            }

            var newFileName = _config.FileNamingStrategy.DetermineBestVersionFilenameFor(chunkHeader.ChunkStartNumber);
            Log.Info("File {0} will be moved to file {1}", oldFileName, newFileName);
            File.Move(oldFileName, newFileName);
            var newChunk = TFChunk.TFChunk.FromCompletedFile(newFileName, verifyHash);
            
            for (int i = chunkHeader.ChunkStartNumber; i <= chunkHeader.ChunkEndNumber; ++i)
            {
                var oldChunk = Interlocked.Exchange(ref _chunks[i], newChunk);
                if (oldChunk != null)
                {
                    oldChunk.MarkForDeletion();
                    Log.Info("Old chunk {0} is marked for deletion.", oldChunk.FileName);
                }
            }

            if (replaceChunksWithGreaterNumbers)
            {
                var oldChunksCount = _chunksCount;
                _chunksCount = newChunk.ChunkHeader.ChunkEndNumber + 1;

                for (int i = chunkHeader.ChunkEndNumber + 1; i < oldChunksCount; ++i)
                {
                    var oldChunk = Interlocked.Exchange(ref _chunks[i], null);
                    if (oldChunk != null)
                    {
                        oldChunk.MarkForDeletion();
                        Log.Info("Excessive chunk {0} is marked for deletion.", oldChunk.FileName);
                    }
                }
                if (_chunks[_chunksCount] != null)
                    throw new Exception(string.Format("Excessive chunk #{0} found after raw replication switch.", _chunksCount));
            }

            TryCacheChunk(newChunk);
            return newChunk;
        }

        public TFChunk.TFChunk CreateTempChunk(ChunkHeader chunkHeader, int fileSize)
        {
            var chunkFileName = _config.FileNamingStrategy.GetTempFilename();
            return TFChunk.TFChunk.CreateWithHeader(chunkFileName, chunkHeader, fileSize);
        }
    }
}