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
using EventStore.BufferManagement;

namespace EventStore.Transport.Tcp.Formatting
{
    /// <summary>
    /// Formats a message for transport using ProtoBuf serialization
    /// </summary>
    public class ProtoBufMessageFormatter<T> : FormatterBase<T>
    {
        private readonly BufferManager _bufferManager;
        private readonly int _initialBuffers;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProtoBufMessageFormatter{T}"/> class.
        /// </summary>
        public ProtoBufMessageFormatter() : this(BufferManager.Default, 2) { }


        /// <summary>
        /// Initializes a new instance of the <see cref="ProtoBufMessageFormatter{T}"/> class.
        /// </summary>
        /// <param name="bufferManager">The buffer manager.</param>
        public ProtoBufMessageFormatter(BufferManager bufferManager) : this(bufferManager, 2) { }


        /// <summary>
        /// Initializes a new instance of the <see cref="ProtoBufMessageFormatter{T}"/> class.
        /// </summary>
        /// <param name="bufferManager">The buffer manager.</param>
        /// <param name="initialBuffers">The number of initial buffers.</param>
        public ProtoBufMessageFormatter(BufferManager bufferManager, int initialBuffers)
        {
            _bufferManager = bufferManager;
            _initialBuffers = initialBuffers;
        }

        /// <summary>
        /// Gets a <see cref="BufferPool"></see> representing the IMessage provided.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>A <see cref="BufferPool"></see> with a representation of the message</returns>
        public override BufferPool ToBufferPool(T message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            var bufferPool = new BufferPool(_initialBuffers, _bufferManager);
            var stream = new BufferPoolStream(bufferPool);
            ProtoBuf.Serializer.Serialize(stream, message);
            return bufferPool;
        }

        /// <summary>
        /// Creates a message object from the specified stream
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>A message object</returns>
        public override T From(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            return ProtoBuf.Serializer.Deserialize<T>(stream);
        }
    }
}