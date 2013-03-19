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
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_writing_an_existing_chunked_transaction_file_with_checksum_and_data_bigger_than_buffer : SpecificationWithDirectory
    {
        private readonly Guid _correlationId = Guid.NewGuid();
        private readonly Guid _eventId = Guid.NewGuid();
        private InMemoryCheckpoint _checkpoint;

        [Test, Ignore("On MONO something strange is happening when the record size is > 4096 bytes, the first bytes are not written into file. Will have to debug later.")]
        public void a_record_can_be_written()
        {
            var filename = GetFilePathFor("prefix.tf0");
            var chunkHeader = new ChunkHeader(TFChunk.CurrentChunkVersion, 10000, 0, 0, false, Guid.NewGuid());
            var chunkBytes = chunkHeader.AsByteArray();
            var buf = new byte[ChunkHeader.Size + ChunkFooter.Size + chunkHeader.ChunkSize];
            Buffer.BlockCopy(chunkBytes, 0, buf, 0, chunkBytes.Length);
            File.WriteAllBytes(filename, buf);

            _checkpoint = new InMemoryCheckpoint(137);
            var db = new TFChunkDb(new TFChunkDbConfig(PathName,
                                                       new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                                       chunkHeader.ChunkSize,
                                                       0,
                                                       _checkpoint,
                                                       new InMemoryCheckpoint(),
                                                       new InMemoryCheckpoint(-1),
                                                       new InMemoryCheckpoint(-1)));
            db.Open();

            var bytes = new byte[3994]; // this gives exactly 4097 size of record, with 3993 (rec size 4096) everything works fine!
            new Random().NextBytes(bytes);
            var writer = new TFChunkWriter(db);
            var record = new PrepareLogRecord(logPosition: 137,
                                              correlationId: _correlationId,
                                              eventId: _eventId,
                                              transactionPosition: 789,
                                              transactionOffset: 543,
                                              eventStreamId: "WorldEnding",
                                              expectedVersion: 1234,
                                              timeStamp: new DateTime(2012, 12, 21),
                                              flags: PrepareFlags.SingleWrite,
                                              eventType: "type",
                                              data: bytes, 
                                              metadata: new byte[] { 0x07, 0x17 });

            long pos;
            Assert.IsTrue(writer.Write(record, out pos));
            writer.Close();
            db.Dispose();

            Assert.AreEqual(record.GetSizeWithLengthPrefixAndSuffix() + 137, _checkpoint.Read());
            using (var filestream = File.Open(filename, FileMode.Open, FileAccess.Read))
            {
                filestream.Seek(ChunkHeader.Size + 137 + sizeof(int), SeekOrigin.Begin);
                var reader = new BinaryReader(filestream);
                var read = LogRecord.ReadFrom(reader);
                Assert.AreEqual(record, read);
            }
        }
    }
}