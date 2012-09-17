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
using EventStore.Core.Util;
using NUnit.Framework;

namespace EventStore.Core.Tests
{
    [TestFixture]
    public class MD5HashTests
    {
         [Test]
         public void does_not_include_previous_data_in_stream()
         {
             var bytes = new byte[1024];
             for (int i=15;i<1024;i++)
             {
                 bytes[i] = (byte) (i%255);
             }
             var stream = new MemoryStream(bytes);
             stream.Seek(16, SeekOrigin.Begin);
             var hash = MD5Hash.GetHashFor(stream);
             Array.Copy(hash, 0, bytes, 0, hash.Length);
             stream.Seek(16, SeekOrigin.Begin);
             var hash2 = MD5Hash.GetHashFor(stream);
             Assert.AreEqual(16, hash.Length);
             Assert.AreEqual(hash, hash2);
         }

         [Test]
         public void changing_data_in_stream_results_in_different_hash()
         {
             var bytes = new byte[1024];
             for (int i = 15; i < 1024; i++)
             {
                 bytes[i] = (byte)(i % 255);
             }
             var stream = new MemoryStream(bytes);
             stream.Seek(16, SeekOrigin.Begin);
             var hash = MD5Hash.GetHashFor(stream);
             bytes[243] = 17;
             stream.Seek(16, SeekOrigin.Begin);
             var hash2 = MD5Hash.GetHashFor(stream);
             Assert.AreNotEqual(hash, hash2);
         }

         [Test]
         public void includes_correct_substream_data()
         {
             var bytes = new byte[1024];
             for (int i = 15; i < 1024; i++)
             {
                 bytes[i] = (byte)(i % 255);
             }
             var stream = new MemoryStream(bytes);
             var hash = MD5Hash.GetHashFor(stream, 16, bytes.Length - 32);

             using (var md5 = MD5.Create())
             {
                 var referenceHash = md5.ComputeHash(bytes, 16, bytes.Length - 32);
                 Assert.AreEqual(16, hash.Length);
                 Assert.AreEqual(referenceHash, hash);
             }
         }

         [Test, Category("LongRunning"), Explicit]
         public void randomized_hash_verification_test()
         {
             var buf = new byte[1024];
             var seed = Environment.TickCount;
             Console.WriteLine("Seed: {0}", seed);
             new Random().NextBytes(buf);

             var stream = new MemoryStream(buf);

             using (var md5 = MD5.Create())
             {
                 for (int i = 0; i < buf.Length; ++i)
                 {
                     for (int j = i; j < buf.Length+10; ++j)
                     {
                         var referenceHash = md5.ComputeHash(buf, i, Math.Min(buf.Length-i, j - i + 1));
                         var hash = MD5Hash.GetHashFor(stream, i, j - i + 1);
                         Assert.AreEqual(16, hash.Length);
                         Assert.AreEqual(referenceHash, hash);
                     }
                 }
             }
         }
    }
}