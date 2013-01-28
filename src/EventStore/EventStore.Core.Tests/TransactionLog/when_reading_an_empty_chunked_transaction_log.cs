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
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_reading_an_empty_chunked_transaction_log : SpecificationWithDirectory
    {
        [Test]
        public void try_read_returns_false_when_writer_checksum_is_zero()
        {
            var writerchk = new InMemoryCheckpoint(0);
            var db = new TFChunkDb(new TFChunkDbConfig(PathName,
                                                       new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                                       10000,
                                                       0,
                                                       writerchk,
                                                       new InMemoryCheckpoint(),
                                                       new InMemoryCheckpoint(-1),
                                                       new InMemoryCheckpoint(-1)));
            db.OpenVerifyAndClean();

            var reader = new TFChunkReader(db, writerchk, 0);
            LogRecord record;
            Assert.IsFalse(reader.TryReadNext(out record));

            db.Close();
        }

        [Test]
        public void try_read_does_not_cache_anything_and_returns_record_once_it_is_written_later()
        {
            var writerchk = new InMemoryCheckpoint(0);
            var db = new TFChunkDb(new TFChunkDbConfig(PathName,
                                                       new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                                       10000,
                                                       0,
                                                       writerchk,
                                                       new InMemoryCheckpoint(),
                                                       new InMemoryCheckpoint(-1),
                                                       new InMemoryCheckpoint(-1)));
            db.OpenVerifyAndClean();

            var writer = new TFChunkWriter(db);
            writer.Open();

            var reader = new TFChunkReader(db, writerchk, 0);

            LogRecord record;
            Assert.IsFalse(reader.TryReadNext(out record));

            var rec = LogRecord.SingleWrite(0, Guid.NewGuid(), Guid.NewGuid(), "ES", -1, "ET", new byte[] { 7 }, null);
            long tmp;
            Assert.IsTrue(writer.Write(rec, out tmp));
            writer.Flush();
            writer.Close();

            Assert.IsTrue(reader.TryReadNext(out record));
            Assert.AreEqual(rec, record);

            db.Close();
        }
    }
}
