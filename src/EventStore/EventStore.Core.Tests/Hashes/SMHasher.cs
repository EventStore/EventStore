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
using EventStore.Core.Index.Hashes;

namespace EventStore.Core.Tests.Hashes
{
    /// <summary>
    /// Adapted Verification and Sanity checks from SMHasher
    /// http://code.google.com/p/smhasher/
    /// </summary>
    public static class SMHasher
    {
        //-----------------------------------------------------------------------------
        // This should hopefully be a thorough and unambiguous test of whether a hash
        // is correctly implemented on a given platform
        public static bool VerificationTest(IHasher hasher, uint expected)
        {
            const int hashBytes = 4;

            var key = new byte[256];
            var hashes = new byte[hashBytes * 256];

            // Hash keys of the form {0}, {0,1}, {0,1,2}... up to N=255,using 256-N as
            // the seed
            for (int i = 0; i < 256; i++)
            {
                key[i] = (byte)i;

                var hash = hasher.Hash(key, 0, (uint)i, (uint)(256 - i));
                Buffer.BlockCopy(BitConverter.GetBytes(hash), 0, hashes, i*hashBytes, hashBytes);
            }

            // Then hash the result array

            var verification = hasher.Hash(hashes, 0, hashBytes * 256, 0);

            // The first four bytes of that hash, interpreted as a little-endian integer, is our
            // verification value

            //uint verification = (final[0] << 0) | (final[1] << 8) | (final[2] << 16) | (final[3] << 24);


            //----------

            if (expected != verification)
            {
                Console.WriteLine("Verification value 0x{0:X8} : Failed! (Expected 0x{1:X8})", verification, expected);
                return false;
            }
            else
            {
                Console.WriteLine("Verification value 0x{0:X8} : Passed!", verification);
                return true;
            }
        }

        //----------------------------------------------------------------------------
        // Basic sanity checks:
        //     - A hash function should not be reading outside the bounds of the key.
        //     - Flipping a bit of a key should, with overwhelmingly high probability, result in a different hash.
        //     - Hashing the same key twice should always produce the same result.
        //     - The memory alignment of the key should not affect the hash result.
        public static bool SanityTest(IHasher hasher)
        {
            var rnd = new Random(883741);

            bool result = true;

            const int reps = 10;
            const int keymax = 256;
            const int pad = 16;
            const int buflen = keymax + pad*3;
  
            byte[] buffer1 = new byte[buflen];
            byte[] buffer2 = new byte[buflen];
            //----------
            for (int irep = 0; irep < reps; irep++)
            {
                if (irep % (reps/10) == 0) Console.Write(".");

                for (int len = 4; len <= keymax; len++)
                {
                    for(int offset = pad; offset < pad*2; offset++)
                    {
//                        byte* key1 = &buffer1[pad];
//                        byte* key2 = &buffer2[pad+offset];

                        rnd.NextBytes(buffer1);
                        rnd.NextBytes(buffer2);

                        //memcpy(key2, key1, len);
                        Buffer.BlockCopy(buffer2, pad + offset, buffer1, pad, len);

                        // hash1 = hash(key1, len, 0)
                        var hash1 = hasher.Hash(buffer1, pad, (uint)len, 0);

                        for(int bit = 0; bit < (len * 8); bit++)
                        {
                            // Flip a bit, hash the key -> we should get a different result.
                            //Flipbit(key2,len,bit);
                            Flipbit(buffer2, pad + offset, len, bit);
                            var hash2 = hasher.Hash(buffer2, pad + offset, (uint)len, 0);

                            if (hash1 == hash2)
                                result = false;

                            // Flip it back, hash again -> we should get the original result.
                            //flipbit(key2,len,bit);
                            Flipbit(buffer2, pad + offset, len, bit);
                            //hash(key2, len, 0, hash2);
                            hash2 = hasher.Hash(buffer2, pad + offset, (uint)len, 0);

                            if (hash1 != hash2)
                                result = false;
                        }
                    }
                }
            }
            return result;
        }

        private static void Flipbit(byte[] array, int offset, int len, int bit)
        {
            var byteNum = bit >> 3;
            bit = bit & 0x7;
            
            if (byteNum < len)
                array[offset + byteNum] ^= (byte)(1 << bit);
        }
    }
}