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
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkDbConfig
    {
        public IEnumerable<ICheckpoint> Checkpoints { get { return _namedCheckpoints.Values; } }

        public readonly string Path;
        public readonly int ChunkSize;
        public readonly int CachedChunkCount;
        public readonly ICheckpoint WriterCheckpoint;
        public readonly ICheckpoint ChaserCheckpoint;
        public readonly IFileNamingStrategy FileNamingStrategy;

        private readonly IDictionary<string, ICheckpoint> _namedCheckpoints;

        public TFChunkDbConfig(string path, 
                               IFileNamingStrategy fileNamingStrategy, 
                               int chunkSize,
                               int cachedChunkCount,
                               ICheckpoint writerCheckpoint, 
                               ICheckpoint chaserCheckpoint,
                               IEnumerable<ICheckpoint> namedCheckpoints)
        {
            Ensure.NotNullOrEmpty(path, "path");
            Ensure.NotNull(fileNamingStrategy, "fileNamingStrategy");
            Ensure.Positive(chunkSize, "chunkSize");
            Ensure.Nonnegative(cachedChunkCount, "cachedChunkCount");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
            Ensure.NotNull(chaserCheckpoint, "chaserCheckpoint");
            Ensure.NotNull(namedCheckpoints, "namedCheckpoints");

//            if ((chunkSize & (chunkSize-1)) != 0)
//                throw new ArgumentException("Segment size should be the power of 2.", "chunkSize");
            
            Path = path;
            ChunkSize = chunkSize;
            CachedChunkCount = cachedChunkCount;
            WriterCheckpoint = writerCheckpoint;
            ChaserCheckpoint = chaserCheckpoint;
            FileNamingStrategy = fileNamingStrategy;
            _namedCheckpoints = namedCheckpoints.ToDictionary(x => x.Name);
        }

        public ICheckpoint GetNamedCheckpoint(string name)
        {
            ICheckpoint item;
            _namedCheckpoints.TryGetValue(name, out item);
            return item;
        }
    }
}