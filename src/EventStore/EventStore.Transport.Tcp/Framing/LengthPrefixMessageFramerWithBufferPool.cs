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
using System.Collections.Generic;
using EventStore.BufferManagement;

namespace EventStore.Transport.Tcp.Framing
{
    public class LengthPrefixMessageFramerWithBufferPool
    {
        private const int HeaderLength = sizeof(Int32);

        private BufferPool _messageBuffer;
        private Action<BufferPool> _receivedHandler;

        private int _headerBytes;
        private int _packageLength;

        /// <summary>
        /// Initializes a new instance of the <see cref="LengthPrefixMessageFramerWithBufferPool"/> class.
        /// </summary>
        public LengthPrefixMessageFramerWithBufferPool()
        {
        }

        public void Reset()
        {
            _messageBuffer = null;
            _headerBytes = 0;
            _packageLength = 0;
        }

        public void UnFrameData(IEnumerable<ArraySegment<byte>> data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            foreach (ArraySegment<byte> buffer in data)
            {
                Parse(buffer);
            }
        }

        public void UnFrameData(ArraySegment<byte> data)
        {
            Parse(data);
        }

        /// <summary>
        /// Parses a stream chunking based on length-prefixed framing. Calls are re-entrant and hold state internally.
        /// </summary>
        /// <param name="bytes">A byte array of data to append</param>
        private void Parse(ArraySegment<byte> bytes)
        {
            byte[] data = bytes.Array;
            for (int i = bytes.Offset; i < bytes.Offset + bytes.Count; )
            {
                if (_headerBytes < HeaderLength)
                {
                    _packageLength |= (data[i] << (_headerBytes * 8)); // little-endian order
                    ++_headerBytes;
                    i += 1;
                    if (_headerBytes == HeaderLength)
                    {
                        if (_packageLength == 0)
                            throw new InvalidOperationException("Package should not be 0 sized.");

                        _messageBuffer = new BufferPool();
                    }
                }
                else
                {
                    int copyCnt = Math.Min(bytes.Count + bytes.Offset - i, _packageLength - _messageBuffer.Length);
                    _messageBuffer.Append(bytes.Array, i, copyCnt);
                    i += copyCnt;

                    if (_messageBuffer.Length == _packageLength)
                    {
                        if (_receivedHandler != null)
                            _receivedHandler(_messageBuffer);
                        _messageBuffer = null;
                        _headerBytes = 0;
                        _packageLength = 0;
                    }
                }
            }
        }

        public IEnumerable<ArraySegment<byte>> FrameData(ArraySegment<byte> data)
        {
            var length = data.Count;

            yield return new ArraySegment<byte>(
                new[] { (byte)length, (byte)(length >> 8), (byte)(length >> 16), (byte)(length >> 24) });
            yield return data;
        }

        public void RegisterMessageArrivedCallback(Action<BufferPool> handler)
        {
            if (handler == null)
                throw new ArgumentNullException("handler");

            _receivedHandler = handler;
        }
    }
}