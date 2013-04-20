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

namespace EventStore.Core.Tests
{
    using System;
    using System.IO;
    using NUnit.Framework;

    [TestFixture, Ignore("Known bug in Mono, waiting for fix.")]
    public class mono_filestream_bug
    {
        [Test]
        public void show_time()
        {
            const int pos = 1;
            const int bufferSize = 128;

            var filename = Path.GetTempFileName();
            File.WriteAllBytes(filename, new byte[pos + 1]); // init file with zeros

            var bytes = new byte[bufferSize + 1 /* THIS IS WHAT MAKES A BIG DIFFERENCE */];
            new Random().NextBytes(bytes);

            using (var file = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite, FileShare.Read,
                                             bufferSize, FileOptions.SequentialScan))
            {
                file.Read(new byte[pos], 0, pos); // THIS READ IS CRITICAL, WITHOUT IT EVERYTHING WORKS
                Assert.AreEqual(pos, file.Position); // !!! here it says position is correct, but writes at different position !!!
                // file.Position = pos; // !!! this fixes test !!!
                file.Write(bytes, 0, bytes.Length);

                //Assert.AreEqual(pos + bytes.Length, file.Length); -- fails
            }

            using (var filestream = File.Open(filename, FileMode.Open, FileAccess.Read))
            {
                var bb = new byte[bytes.Length];
                filestream.Position = pos;
                filestream.Read(bb, 0, bb.Length);
                Assert.AreEqual(bytes, bb);
            }
        }
    }
}

