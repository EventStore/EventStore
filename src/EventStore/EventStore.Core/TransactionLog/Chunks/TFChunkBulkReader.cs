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
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkBulkReader : IDisposable
    {
        public TFChunk.TFChunk Chunk { get { return _chunk; } }

        private readonly TFChunk.TFChunk _chunk;
        private readonly Stream _stream;
        private bool _disposed;

        internal TFChunkBulkReader(TFChunk.TFChunk chunk, Stream streamToUse)
        {
            Ensure.NotNull(chunk, "chunk");
            Ensure.NotNull(streamToUse, "stream");
            _chunk = chunk;
            _stream = streamToUse;
        }

        ~TFChunkBulkReader()
        {
            Dispose();
        }

        public void SetRawPosition(int rawPosition)
        {
            if (rawPosition >= _stream.Length)
                throw new ArgumentOutOfRangeException("rawPosition", string.Format("Raw position {0} is out of bounds.", rawPosition));
            _stream.Position = rawPosition;
        }

        public void SetDataPosition(long dataPosition)
        {
            var rawPos = dataPosition + ChunkHeader.Size;
            if (rawPos >= _stream.Length)
                throw new ArgumentOutOfRangeException("dataPosition", string.Format("Data position {0} is out of bounds.", dataPosition));
            _stream.Position = rawPos;
        }

        public void Release()
        {
            _stream.Close();
            _stream.Dispose();
            _disposed = true;
            _chunk.ReleaseReader(this);
        }

        public BulkReadResult ReadNextRawBytes(int count, byte[] buffer)
        {
            Ensure.NotNull(buffer, "buffer");
            Ensure.Nonnegative(count, "count");

            if (count > buffer.Length)
                count = buffer.Length;

            var oldPos = (int)_stream.Position;
            int bytesRead = _stream.Read(buffer, 0, count);
            return new BulkReadResult(oldPos, bytesRead, isEof: _stream.Length == _stream.Position);
        }

        public BulkReadResult ReadNextDataBytes(int count, byte[] buffer)
        {
            Ensure.NotNull(buffer, "buffer");
            Ensure.Nonnegative(count, "count");

            if (_stream.Position == 0)
                _stream.Position = ChunkHeader.Size;

            if (count > buffer.Length)
                count = buffer.Length;

            var oldPos = (int)_stream.Position - ChunkHeader.Size;
            var toRead = Math.Min(_chunk.PhysicalDataSize - oldPos, count);
            Debug.Assert(toRead >= 0);
            _stream.Position = _stream.Position; // flush read buffer
            int bytesRead = _stream.Read(buffer, 0, toRead);
            return new BulkReadResult(oldPos,
                                      bytesRead,
                                      isEof: _chunk.IsReadOnly && oldPos + bytesRead == _chunk.PhysicalDataSize);
        }

        public void Dispose()
        {
            if(_disposed) 
                return;
            Release();
            GC.SuppressFinalize(this);
        }

    }
}