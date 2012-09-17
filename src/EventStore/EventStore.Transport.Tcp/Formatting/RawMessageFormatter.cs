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
using EventStore.BufferManagement;

namespace EventStore.Transport.Tcp.Formatting
{
    /// <summary>
    /// Formatter which doesn't format anything, actually. Just outputs raw byte[].
    /// </summary>
    public class RawMessageFormatter : IMessageFormatter<byte[]>
    {
        private readonly BufferManager _bufferManager;
        private readonly int _initialBuffers;

        /// <summary>
        /// Initializes a new instance of the <see cref="RawMessageFormatter"/> class.
        /// </summary>
        public RawMessageFormatter() : this(BufferManager.Default, 2) { }


        /// <summary>
        /// Initializes a new instance of the <see cref="RawMessageFormatter"/> class.
        /// </summary>
        /// <param name="bufferManager">The buffer manager.</param>
        public RawMessageFormatter(BufferManager bufferManager) : this(bufferManager, 2) { }


        /// <summary>
        /// Initializes a new instance of the <see cref="RawMessageFormatter"/> class.
        /// </summary>
        /// <param name="bufferManager">The buffer manager.</param>
        /// <param name="initialBuffers">The number of initial buffers.</param>
        public RawMessageFormatter(BufferManager bufferManager, int initialBuffers)
        {
            _bufferManager = bufferManager;
            _initialBuffers = initialBuffers;
        }

        public BufferPool ToBufferPool(byte[] message)
        {
            if (message == null) throw new ArgumentNullException("message");

            var bufferPool = new BufferPool(_initialBuffers, _bufferManager);
            var stream = new BufferPoolStream(bufferPool);
            stream.Write(message, 0, message.Length);
            return bufferPool;
        }

        public ArraySegment<byte> ToArraySegment(byte[] message)
        {
            if (message == null) throw new ArgumentNullException("message");
            return new ArraySegment<byte>(message, 0, message.Length);
        }

        public byte[] ToArray(byte[] message)
        {
            if (message == null) throw new ArgumentNullException("message");
            return message;
        }

        public byte[] From(BufferPool bufferPool)
        {
            return bufferPool.ToByteArray();
        }

        public byte[] From(ArraySegment<byte> segment)
        {
            var msg = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array, segment.Offset, msg, 0, segment.Count);
            return msg;
        }

        public byte[] From(byte[] array)
        {
            return array;
        }
    }
}