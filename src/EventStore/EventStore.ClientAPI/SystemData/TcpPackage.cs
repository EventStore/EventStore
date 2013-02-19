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

namespace EventStore.ClientAPI.SystemData
{
    public struct TcpPackage
    {
        private static readonly byte[] EmptyArray = new byte[0];

        public readonly TcpCommand Command;
        public readonly Guid CorrelationId;
        public readonly ArraySegment<byte> Data;

        public static TcpPackage FromArraySegment(ArraySegment<byte> data)
        {
            if (data.Count < 17)
                throw new ArgumentException(string.Format("ArraySegment too short, length: {0}", data.Count), "data");

            var guidBytes = new byte[16];
            Buffer.BlockCopy(data.Array, data.Offset + 1, guidBytes, 0, 16);
            var correlationId = new Guid(guidBytes);
            return new TcpPackage((TcpCommand)data.Array[data.Offset],
                                  correlationId,
                                  new ArraySegment<byte>(data.Array, data.Offset + 17, data.Count - 17));
        }

        public TcpPackage(TcpCommand command, Guid correlationId, byte[] data)
        {
            Command = command;
            CorrelationId = correlationId;
            Data = new ArraySegment<byte>(data ?? EmptyArray);
        }

        public TcpPackage(TcpCommand command, Guid correlationId, ArraySegment<byte> data)
        {
            CorrelationId = correlationId;
            Command = command;
            Data = data;
        }

        public byte[] AsByteArray()
        {
            var res = new byte[Data.Count + 17];
            res[0] = (byte)Command;
            Buffer.BlockCopy(CorrelationId.ToByteArray(), 0, res, 1, 16);
            Buffer.BlockCopy(Data.Array, Data.Offset, res, 17, Data.Count);
            return res;
        }

        public ArraySegment<byte> AsArraySegment()
        {
            return new ArraySegment<byte>(AsByteArray());
        }
    }
}
