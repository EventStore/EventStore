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
using System.IO;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.MultifileTransactionFile;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_reading_bulk_records: SpecificationWithDirectory
    {
        [Test]
        public void should_not_read_buffered_data()
        {
            var writerchk = new InMemoryCheckpoint(0);
            var readerchk = new InMemoryCheckpoint("reader", 0);
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, writerchk, new List<ICheckpoint> {readerchk});

            var fileName = Path.Combine(PathName, "prefix.tf0");
            File.Create(fileName).Close();

            var reader = new MultifileTransactionFileBulkRetriever(config);
            reader.Open(0);
            
            Assert.IsTrue(reader.ReadNextBulk().Length == 0);

            var bytes = new byte[100];
            new Random().NextBytes(bytes);

            using (var f = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                f.Write(bytes, 0, bytes.Length);
                f.Flush(flushToDisk: true);
            }
            writerchk.Write(bytes.Length);

            var readBytes = reader.ReadNextBulk();
            Assert.AreEqual(bytes, readBytes);

            bytes = new byte[100];
            new Random().NextBytes(bytes);

            using (var f = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                f.Write(bytes, 0, bytes.Length);
                f.Flush(flushToDisk: true);
            }
            writerchk.Write(writerchk.Read() + bytes.Length);

            readBytes = reader.ReadNextBulk();
            Assert.AreEqual(bytes, readBytes);

            reader.Close();
        }
            
    }
}