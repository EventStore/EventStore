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
using System.Security.Cryptography;
using EventStore.Common.Utils;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk
{
    internal class WriterWorkItem
    {
        public Stream WorkingStream { get { return _workingStream; } }
        public long StreamLength { get { return _workingStream.Length; } }
        public long StreamPosition { get { return _workingStream.Position; } }

        private readonly FileStream _fileStream;
        private UnmanagedMemoryStream _memStream;
        private Stream _workingStream;

        public readonly MemoryStream Buffer;
        public readonly BinaryWriter BufferWriter;
        public readonly MD5 MD5;

        public WriterWorkItem(FileStream fileStream, UnmanagedMemoryStream memStream, MD5 md5)
        {
            _fileStream = fileStream;
            _memStream = memStream;
            _workingStream = (Stream)fileStream ?? memStream;
            Buffer = new MemoryStream(8192);
            BufferWriter = new BinaryWriter(Buffer);
            MD5 = md5;
        }

        public void SetMemStream(UnmanagedMemoryStream memStream)
        {
            _memStream = memStream;
            if (_fileStream == null)
                _workingStream = memStream;
        }

        public void AppendData(byte[] buf, int offset, int len)
        {
            // as we are always append-only, stream's position should be right here
            if (_fileStream != null)
                _fileStream.Write(buf, 0, len); 
            //MEMORY
            var memStream = _memStream;
            if (memStream != null)
                memStream.Write(buf, 0, len);
        }

        public void FlushToDisk()
        {
            if (_fileStream != null)
                _fileStream.FlushToDisk();
        }

        public void ResizeStream(int fileSize)
        {
            if (_fileStream != null)
                _fileStream.SetLength(fileSize);
            var memStream = _memStream;
            if (memStream != null)
                memStream.SetLength(fileSize);
        }

        public void Dispose()
        {
            if (_fileStream != null)
                _fileStream.Dispose();

            DisposeMemStream();
        }

        public void DisposeMemStream()
        {
            var memStream = _memStream;
            if (memStream != null)
            {
                memStream.Dispose();
                _memStream = null;
            }
        }

        public void EnsureMemStreamLength(long length)
        {
            throw new NotSupportedException("Scavenging with in-mem DB is not supported.");
        }
    }
}