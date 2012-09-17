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
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class IndexEntryTests
    {
        [Test]
        public void key_is_made_of_stream_and_version()
        {
            var entry = new IndexEntry {Stream = 0x01, Version = 0x12};
            Assert.AreEqual(0x0000000100000012, entry.Key);
        }
        [Test]
        public void bytes_is_made_of_key_and_position()
        {
            unsafe
            {
                var entry = new IndexEntry {Stream = 0x0101, Version = 0x1234, Position = 0xFFFF};
                Assert.AreEqual(0x34, entry.Bytes[0]);
                Assert.AreEqual(0x12, entry.Bytes[1]);
                Assert.AreEqual(0x00, entry.Bytes[2]);
                Assert.AreEqual(0x00, entry.Bytes[3]);
                Assert.AreEqual(0x01, entry.Bytes[4]);
                Assert.AreEqual(0x01, entry.Bytes[5]);
                Assert.AreEqual(0x00, entry.Bytes[6]);
                Assert.AreEqual(0x00, entry.Bytes[7]);
                Assert.AreEqual(0xFF, entry.Bytes[8]);
                Assert.AreEqual(0xFF, entry.Bytes[9]);
                Assert.AreEqual(0x00, entry.Bytes[10]);
                Assert.AreEqual(0x00, entry.Bytes[11]);
                Assert.AreEqual(0x00, entry.Bytes[12]);
                Assert.AreEqual(0x00, entry.Bytes[13]);
                Assert.AreEqual(0x00, entry.Bytes[14]);
                Assert.AreEqual(0x00, entry.Bytes[15]);
            }
        }
    }
}