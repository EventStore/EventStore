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
using EventStore.Core.Exceptions;
using EventStore.Core.Index;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class ChunkHeader
    {
        public const int Size = 128;

        public long ChunkFullSize { get { return (ChunkEndNumber - ChunkStartNumber + 1) * (long)ChunkSize; } }
        public long ChunkStartPosition { get { return ChunkStartNumber * (long)ChunkSize; } }
        public long ChunkEndPosition { get { return (ChunkEndNumber + 1) * (long)ChunkSize; } }

        public readonly byte Version;
        public readonly int ChunkSize;
        public readonly int ChunkStartNumber;
        public readonly int ChunkEndNumber;
        public readonly bool IsScavenged; // uses 4 bytes (legacy)
        public readonly Guid ChunkId;

        public ChunkHeader(byte version, int chunkSize, int chunkStartNumber, int chunkEndNumber, bool isScavenged, Guid chunkId)
        {
            Ensure.Nonnegative(version, "version");
            Ensure.Positive(chunkSize, "chunkSize");
            Ensure.Nonnegative(chunkStartNumber, "chunkStartNumber");
            Ensure.Nonnegative(chunkEndNumber, "chunkEndNumber");
            if (chunkStartNumber > chunkEndNumber)
                throw new ArgumentOutOfRangeException("chunkStartNumber", "chunkStartNumber is greater than ChunkEndNumber.");

            Version = version;
            ChunkSize = chunkSize;
            ChunkStartNumber = chunkStartNumber;
            ChunkEndNumber = chunkEndNumber;
            IsScavenged = isScavenged;
            ChunkId = chunkId;
        }

        public byte[] AsByteArray()
        {
            var array = new byte[Size];
            using (var memStream = new MemoryStream(array))
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write((byte)FileType.ChunkFile);
                writer.Write(Version);
                writer.Write(ChunkSize);
                writer.Write(ChunkStartNumber);
                writer.Write(ChunkEndNumber);
                writer.Write(IsScavenged ? 1 : 0);
                writer.Write(ChunkId.ToByteArray());
            }
            return array;
        }

        public static ChunkHeader FromStream(Stream stream)
        {
            var reader = new BinaryReader(stream);
            
            var fileType = (FileType) reader.ReadByte();
            if (fileType != FileType.ChunkFile)
                throw new CorruptDatabaseException(new InvalidFileException());

            var version = reader.ReadByte();
            var chunkSize = reader.ReadInt32();
            var chunkStartNumber = reader.ReadInt32();
            var chunkEndNumber = reader.ReadInt32();
            var isScavenged = reader.ReadInt32() > 0;
            var chunkId = new Guid(reader.ReadBytes(16));
            return new ChunkHeader(version, chunkSize, chunkStartNumber, chunkEndNumber, isScavenged, chunkId);
        }

        // TODO AN this should be long
        public int GetChunkLocalLogicalPosition(long globalLogicalPosition)
        {
            Ensure.Nonnegative(globalLogicalPosition, "globalLogicalPosition");
            if (globalLogicalPosition < ChunkStartPosition || globalLogicalPosition > ChunkEndPosition)
                throw new Exception(string.Format("globalLogicalPosition {0} is out of chunk logical positions [{1}, {2}].",
                                                  globalLogicalPosition, ChunkStartPosition, ChunkEndPosition));
            return (int)(globalLogicalPosition - ChunkStartPosition);
        }

        public override string ToString()
        {
            return string.Format("Version: {0}, ChunkSize: {1}, ChunkStartNumber: {2}, ChunkEndNumber: {3}, IsScavenged: {4}, ChunkId: {5}\n" +
                                 "ChunkFullSize: {6}, ChunkStartPosition: {7}, ChunkEndPosition: {8}",
                                 Version,
                                 ChunkSize,
                                 ChunkStartNumber,
                                 ChunkEndNumber,
                                 IsScavenged,
                                 ChunkId,
                                 ChunkFullSize,
                                 ChunkStartNumber,
                                 ChunkEndNumber);
        }
    }
}