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
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_writing_an_existing_chunked_transaction_file_with_not_enough_space_in_chunk : SpecificationWithDirectory
    {
        private readonly Guid _correlationId = Guid.NewGuid();
        private readonly Guid _eventId = Guid.NewGuid();
        private InMemoryCheckpoint _checkpoint;

        [Test]
        public void a_record_is_not_written_at_first_but_written_on_second_try()
        {
            var filename1 = GetFilePathFor("prefix.tf0");
            var filename2 = GetFilePathFor("prefix.tf1");
            var chunkHeader = new ChunkHeader(TFChunk.CurrentChunkVersion, 10000, 0, 0, false);
            var chunkBytes = chunkHeader.AsByteArray();
            var bytes = new byte[ChunkHeader.Size + 10000 + ChunkFooter.Size];
            Buffer.BlockCopy(chunkBytes, 0, bytes, 0, chunkBytes.Length);
            File.WriteAllBytes(filename1, bytes);

            _checkpoint = new InMemoryCheckpoint(0);
            var db = new TFChunkDb(new TFChunkDbConfig(PathName,
                                                       new PrefixFileNamingStrategy(PathName, "prefix.tf"),
                                                       10000,
                                                       0,
                                                       _checkpoint,
                                                       new InMemoryCheckpoint(),
                                                       new ICheckpoint[0]));
            db.OpenVerifyAndClean();
            var tf = new TFChunkWriter(db);
            long pos;

            var record1 = new PrepareLogRecord(logPosition: 0,
                                               correlationId: _correlationId,
                                               eventId: _eventId,
                                               expectedVersion: 1234,
                                               transactionPosition: 0,
                                               transactionOffset: 0,
                                               eventStreamId: "WorldEnding",
                                               timeStamp: new DateTime(2012, 12, 21),
                                               flags: PrepareFlags.None,
                                               eventType: "type",
                                               data: new byte[] { 1, 2, 3, 4, 5 },
                                               metadata: new byte[8000]);
            Assert.IsTrue(tf.Write(record1, out pos)); // almost fill up first chunk

            var record2 = new PrepareLogRecord(logPosition: pos,
                                               correlationId: _correlationId,
                                               eventId: _eventId,
                                               expectedVersion: 1234,
                                               transactionPosition: pos,
                                               transactionOffset: 0,
                                               eventStreamId: "WorldEnding",
                                               timeStamp: new DateTime(2012, 12, 21),
                                               flags: PrepareFlags.None,
                                               eventType: "type",
                                               data: new byte[] { 1, 2, 3, 4, 5 },
                                               metadata: new byte[8000]);
            Assert.IsFalse(tf.Write(record2, out pos)); // chunk has too small space

            var record3 = new PrepareLogRecord(logPosition: pos,
                                               correlationId: _correlationId,
                                               eventId: _eventId,
                                               expectedVersion: 1234,
                                               transactionPosition: pos,
                                               transactionOffset: 0,
                                               eventStreamId: "WorldEnding",
                                               timeStamp: new DateTime(2012, 12, 21),
                                               flags: PrepareFlags.None,
                                               eventType: "type",
                                               data: new byte[] { 1, 2, 3, 4, 5 },
                                               metadata: new byte[2000]);
            Assert.IsTrue(tf.Write(record3, out pos));
            tf.Close();
            db.Dispose();

            Assert.AreEqual(record3.GetSizeWithLengthPrefixAndSuffix() + 10000, _checkpoint.Read());
            using (var filestream = File.Open(filename2, FileMode.Open, FileAccess.Read))
            {
                filestream.Seek(ChunkHeader.Size + sizeof(int), SeekOrigin.Begin);
                var reader = new BinaryReader(filestream);
                var read = LogRecord.ReadFrom(reader);
                Assert.AreEqual(record3, read);
            }
        }
    }
}