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

namespace EventStore.BufferManagement
{
    public class BufferPoolStream : Stream
    {
        private readonly BufferPool _bufferPool;
        private long _position;

        public BufferPool BufferPool { get { return _bufferPool; } }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { return _bufferPool.Length; } }
        public int Capacity { get { return _bufferPool.Capacity; } }

        public override long Position
        {
            get { return _position; }
            set
            {
                if (value < 0 || value > _bufferPool.Length)
                    throw new ArgumentOutOfRangeException("value");
                _position = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BufferPoolStream"/> class.
        /// </summary>
        /// <param name="bufferPool">The buffer pool used as underlying storage.</param>
        public BufferPoolStream(BufferPool bufferPool)
        {
            if (bufferPool == null) 
                throw new ArgumentNullException("bufferPool");
            _bufferPool = bufferPool;
        }

        public override void Flush()
        {
            //noop
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch(origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.End:
                    Position = _bufferPool.Length + offset;
                    break;
                case SeekOrigin.Current:
                    Position = _position + offset;
                    break;
                default:
                    throw new Exception("Unknown SeekOrigin: " + origin.ToString());
            }
            return Position;
        }

        public override void SetLength(long value)
        {
            _bufferPool.SetLength((int) value);
            if (_position > value)
                _position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _bufferPool.Length) 
                return 0;
            int ret = _bufferPool.ReadFrom((int) _position, buffer, offset, count);
            _position += ret;
            return ret;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _bufferPool.Write((int) _position, buffer, offset, count);
            _position += count;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _bufferPool.Dispose();
            base.Dispose(disposing);
        }
    }
}