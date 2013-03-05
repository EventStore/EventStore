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
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class ChunkFooter
    {
        public const int Size = 128;
        public const int ChecksumSize = 16;

        public readonly bool IsCompleted;
        public readonly int PhysicalDataSize; // the size of a section of data in chunk
        public readonly int LogicalDataSize;  // the size of a logical data size (after scavenge LogicalDataSize can be > physicalDataSize)
        public readonly int MapSize;
        public readonly byte[] MD5Hash;

        public readonly int MapCount; // calculated, not stored

        public ChunkFooter(bool isCompleted, int physicalDataSize, int logicalDataSize, int mapSize, byte[] md5Hash)
        {
            Ensure.Nonnegative(physicalDataSize, "physicalDataSize");
            Ensure.Nonnegative(logicalDataSize, "logicalDataSize");
            if (logicalDataSize < physicalDataSize)
                throw new ArgumentOutOfRangeException("logicalDataSize", string.Format("LogicalDataSize {0} is less than PhysicalDataSize {1}", logicalDataSize, physicalDataSize));
            Ensure.Nonnegative(mapSize, "mapSize");
            Ensure.NotNull(md5Hash, "md5Hash");
            if (md5Hash.Length != ChecksumSize)
                throw new ArgumentException("MD5Hash is of wrong length.", "md5Hash");

            IsCompleted = isCompleted;
            PhysicalDataSize = physicalDataSize;
            LogicalDataSize = logicalDataSize;
            MapSize = mapSize;
            MD5Hash = md5Hash;

            if (MapSize % PosMap.Size != 0)
                throw new Exception(string.Format("Wrong MapSize {0} -- not divisible by PosMap.Size {1}.", MapSize, PosMap.Size));
            MapCount = mapSize / PosMap.Size;
        }

        public byte[] AsByteArray()
        {
            var array = new byte[Size];
            using (var memStream = new MemoryStream(array))
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write(IsCompleted);
                writer.Write(PhysicalDataSize);
                writer.Write(LogicalDataSize);
                writer.Write(MapSize);
                
                memStream.Position = Size - ChecksumSize;
                writer.Write(MD5Hash);
            }
            return array;
        }

        public static ChunkFooter FromStream(Stream stream)
        {
            var reader = new BinaryReader(stream);
            var completed = reader.ReadBoolean();
            var physicalDataSize = reader.ReadInt32();
            var logicalDataSize = reader.ReadInt32();
            var mapSize = reader.ReadInt32();
            
            stream.Position = stream.Length - ChecksumSize;
            var hash = reader.ReadBytes(ChecksumSize);

            return new ChunkFooter(completed, physicalDataSize, logicalDataSize, mapSize, hash);
        }
    }
}