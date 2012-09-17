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

namespace EventStore.Core.Util
{
    public class MD5Hash
    {
        private static readonly byte[] EmptyArray = new byte[0];

        public static byte[] GetHashFor(Stream s)
        {
            //when using this, it will calculate from this point to the END of the stream!
            using (MD5 md5 = MD5.Create())
                return md5.ComputeHash(s);
        }

        public static byte[] GetHashFor(Stream s, int startPosition, long count)
        {
            Ensure.Nonnegative(count, "count");

            using (MD5 md5 = MD5.Create())
            {
                ContinuousHashFor(md5, s, startPosition, count);
                md5.TransformFinalBlock(EmptyArray, 0, 0);
                return md5.Hash;
            }
        }

        public static void ContinuousHashFor(MD5 md5, Stream s, int startPosition, long count)
        {
            Ensure.NotNull(md5, "md5");
            Ensure.Nonnegative(count, "count");

            if (s.Position != startPosition)
                s.Position = startPosition;

            var buffer = new byte[4096];
            long toRead = count;
            while (toRead > 0)
            {
                int read = s.Read(buffer, 0, (int)Math.Min(toRead, buffer.Length));
                if (read == 0)
                    break;

                md5.TransformBlock(buffer, 0, read, null, 0);
                toRead -= read;
            }
        }
    }
}