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
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkDbConfig
    {
        public readonly string Path;
        public readonly int ChunkSize;
        public readonly long MaxChunksCacheSize;
        public readonly ICheckpoint WriterCheckpoint;
        public readonly ICheckpoint ChaserCheckpoint;
        public readonly ICheckpoint EpochCheckpoint;
        public readonly ICheckpoint TruncateCheckpoint;
        public readonly IFileNamingStrategy FileNamingStrategy;

        public TFChunkDbConfig(string path, 
                               IFileNamingStrategy fileNamingStrategy, 
                               int chunkSize,
                               long maxChunksCacheSize,
                               ICheckpoint writerCheckpoint, 
                               ICheckpoint chaserCheckpoint,
                               ICheckpoint epochCheckpoint,
                               ICheckpoint truncateCheckpoint)
        {
            Ensure.NotNullOrEmpty(path, "path");
            Ensure.NotNull(fileNamingStrategy, "fileNamingStrategy");
            Ensure.Positive(chunkSize, "chunkSize");
            Ensure.Nonnegative(maxChunksCacheSize, "maxChunksCacheSize");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
            Ensure.NotNull(chaserCheckpoint, "chaserCheckpoint");
            Ensure.NotNull(epochCheckpoint, "epochCheckpoint");
            Ensure.NotNull(truncateCheckpoint, "truncateCheckpoint");
            
            Path = path;
            ChunkSize = chunkSize;
            MaxChunksCacheSize = maxChunksCacheSize;
            WriterCheckpoint = writerCheckpoint;
            ChaserCheckpoint = chaserCheckpoint;
            EpochCheckpoint = epochCheckpoint;
            TruncateCheckpoint = truncateCheckpoint;
            FileNamingStrategy = fileNamingStrategy;
        }
    }
}