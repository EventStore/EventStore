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
    public interface IMessageFormatter<T>
    {
        /// <summary>
        /// Converts the object to a <see cref="BufferPool"></see> representing a binary format of it.
        /// </summary>
        /// <param name="message">The message to convert.</param>
        /// <returns>A <see cref="BufferPool"></see> containing the data representing the object.</returns>
        BufferPool ToBufferPool(T message);

        /// <summary>
        /// Converts the object to a <see cref="ArraySegment{Byte}"></see> representing a binary format of it.
        /// </summary>
        /// <param name="message">The message to convert.</param>
        /// <returns>A <see cref="BufferPool"></see> containing the data representing the object.</returns>
        ArraySegment<byte> ToArraySegment(T message);

        /// <summary>
        /// Converts the object to a byte array representing a binary format of it.
        /// </summary>
        /// <param name="message">The message to convert.</param>
        /// <returns>A <see cref="BufferPool"></see> containing the data representing the object.</returns>
        byte[] ToArray(T message);

        /// <summary>
        /// Takes a <see cref="BufferPool"></see> and converts its contents to a message object
        /// </summary>
        /// <param name="bufferPool">The buffer pool.</param>
        /// <returns>A message representing the data given</returns>
        T From(BufferPool bufferPool);

        /// <summary>
        /// Takes an ArraySegment and converts its contents to a message object
        /// </summary>
        /// <param name="segment">The buffer pool.</param>
        /// <returns>A message representing the data given</returns>
        T From(ArraySegment<byte> segment);

        /// <summary>
        /// Takes an Array and converts its contents to a message object
        /// </summary>
        /// <param name="array">The buffer pool.</param>
        /// <returns>A message representing the data given</returns>
        T From(byte [] array);
    }
}
