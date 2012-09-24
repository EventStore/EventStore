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
using ProtoBuf;

namespace EventStore.ClientAPI.Transport.Tcp
{
    static class ProtobufExtensions
    {
        public static T Deserialize<T>(this ArraySegment<byte> data)
        {
            using (var memory = new MemoryStream(data.Array, data.Offset, data.Count))
            {
                var res = Serializer.Deserialize<T>(memory);
                return res;
            }
        }

        public static T Deserialize<T>(this byte[] data)
        {
            using (var memory = new MemoryStream(data))
            {
                var res = Serializer.Deserialize<T>(memory);
                return res;
            }
        }

        public static ArraySegment<byte> Serialize<T>(this T protoContract)
        {
            using (var memory = new MemoryStream())
            {
                Serializer.Serialize(memory, protoContract);
                var res = new ArraySegment<byte>(memory.GetBuffer(), 0, (int) memory.Length);
                return res;
            }
        }
    }
}
