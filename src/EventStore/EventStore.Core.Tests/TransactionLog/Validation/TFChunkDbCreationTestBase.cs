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
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.Tests.TransactionLog.Validation
{
    public static class DbUtil
    {
        public static void CreateSingleChunk(TFChunkDbConfig config, int chunkNum, string filename, int? actualDataSize = null, bool isScavenged = false)
        {
            var chunkHeader = new ChunkHeader(TFChunk.CurrentChunkVersion, config.ChunkSize, chunkNum, chunkNum, isScavenged, Guid.NewGuid());
            var chunkBytes = chunkHeader.AsByteArray();
            var dataSize = actualDataSize ?? config.ChunkSize;
            var buf = new byte[ChunkHeader.Size + dataSize + ChunkFooter.Size];
            Buffer.BlockCopy(chunkBytes, 0, buf, 0, chunkBytes.Length);
            var chunkFooter = new ChunkFooter(true, true, dataSize, dataSize, 0, new byte[ChunkFooter.ChecksumSize]);
            chunkBytes = chunkFooter.AsByteArray();
            Buffer.BlockCopy(chunkBytes, 0, buf, buf.Length - ChunkFooter.Size, chunkBytes.Length);
            File.WriteAllBytes(filename, buf);
        }

        public static void CreateMultiChunk(TFChunkDbConfig config, int chunkStartNum, int chunkEndNum, string filename,
                                            int? physicalSize = null, long? logicalSize = null)
        {
            if (chunkStartNum > chunkEndNum) throw new ArgumentException("chunkStartNum");

            var chunkHeader = new ChunkHeader(TFChunk.CurrentChunkVersion, config.ChunkSize, chunkStartNum, chunkEndNum, true, Guid.NewGuid());
            var chunkBytes = chunkHeader.AsByteArray();
            var physicalDataSize = physicalSize ?? config.ChunkSize;
            var logicalDataSize = logicalSize ?? (chunkEndNum - chunkStartNum + 1) * config.ChunkSize;
            var buf = new byte[ChunkHeader.Size + physicalDataSize + ChunkFooter.Size];
            Buffer.BlockCopy(chunkBytes, 0, buf, 0, chunkBytes.Length);
            var chunkFooter = new ChunkFooter(true, true, physicalDataSize, logicalDataSize, 0, new byte[ChunkFooter.ChecksumSize]);
            chunkBytes = chunkFooter.AsByteArray();
            Buffer.BlockCopy(chunkBytes, 0, buf, buf.Length - ChunkFooter.Size, chunkBytes.Length);
            File.WriteAllBytes(filename, buf);
        }

        public static void CreateOngoingChunk(TFChunkDbConfig config, int chunkNum, string filename, int? actualSize = null)
        {
            var chunkHeader = new ChunkHeader(TFChunk.CurrentChunkVersion, config.ChunkSize, chunkNum, chunkNum, false, Guid.NewGuid());
            var chunkBytes = chunkHeader.AsByteArray();
            var buf = new byte[ChunkHeader.Size + (actualSize ?? config.ChunkSize) + ChunkFooter.Size];
            Buffer.BlockCopy(chunkBytes, 0, buf, 0, chunkBytes.Length);
            File.WriteAllBytes(filename, buf);
        }        
    }
}