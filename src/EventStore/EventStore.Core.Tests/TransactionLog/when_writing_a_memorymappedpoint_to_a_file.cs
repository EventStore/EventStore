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

using System;
using System.Threading;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_writing_a_memorymappedpoint_to_a_file : SpecificationWithFile
    {
        [Test]
        public void a_null_file_throws_argumentnullexception()
        {
            Assert.Throws<ArgumentNullException>(() => new MemoryMappedFileCheckpoint(null));
        }

        [Test]
        public void name_is_set()
        {
            var checksum = new MemoryMappedFileCheckpoint(Filename, "test", false);
            Assert.AreEqual("test", checksum.Name);
            checksum.Close();
        }

        [Test]
        public void reading_off_same_instance_gives_most_up_to_date_info()
        {
            var checkSum = new MemoryMappedFileCheckpoint(Filename);
            checkSum.Write(0xDEAD);
            checkSum.Flush();
            var read = checkSum.Read();
            checkSum.Close();
            Assert.AreEqual(0xDEAD, read);
        }

        [Test]
        public void can_read_existing_checksum()
        {
            var checksum = new MemoryMappedFileCheckpoint(Filename);
            checksum.Write(0xDEAD);
            checksum.Close();
            checksum = new MemoryMappedFileCheckpoint(Filename);
            var val = checksum.Read();
            checksum.Close();
            Assert.AreEqual(0xDEAD, val);
        }

        [Test]
        public void the_new_value_is_not_accessible_if_not_flushed_even_with_delay()
        {
                var checkSum = new MemoryMappedFileCheckpoint(Filename);
                var readChecksum = new MemoryMappedFileCheckpoint(Filename);
                checkSum.Write(1011);
                Thread.Sleep(200);
                Assert.AreEqual(0, readChecksum.Read());
                checkSum.Close();
                readChecksum.Close();
        }

        [Test]
        public void the_new_value_is_accessible_after_flush()
        {
                var checkSum = new MemoryMappedFileCheckpoint(Filename);
                var readChecksum = new MemoryMappedFileCheckpoint(Filename);
                checkSum.Write(1011);
                checkSum.Flush();
                Assert.AreEqual(1011, readChecksum.Read());
                checkSum.Close();
                readChecksum.Close();
                Thread.Sleep(100);
        }
    }
}
