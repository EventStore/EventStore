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
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.TransactionLog.MultifileTransactionFile;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_writing_an_existing_multifile_transaction_file_with_checksum_and_data_bigger_than_buffer : SpecificationWithDirectory
    {
        private readonly Guid _correlationId = Guid.NewGuid();
        private readonly Guid _eventId = Guid.NewGuid();
        private InMemoryCheckpoint _checkpoint;

        [Test]
        public void a_record_can_be_written()
        {
            var filename = Path.Combine(PathName, "prefix.tf0");
            File.WriteAllBytes(filename, new byte[10000]);
            _checkpoint = new InMemoryCheckpoint(137);
            var tf = new MultifileTransactionFileWriter(
                new TransactionFileDatabaseConfig(PathName, "prefix.tf", 100000, _checkpoint, new List<ICheckpoint>()));
            tf.Open();
            var record = new PrepareLogRecord(logPosition: 0,
                                              correlationId: _correlationId,
                                              eventId: _eventId,
                                              transactionPosition: 0,
                                              eventStreamId: "WorldEnding",
                                              expectedVersion: 1234,
                                              timeStamp: new DateTime(2012, 12, 21),
                                              flags: PrepareFlags.None,
                                              eventType: "type",
                                              data: new byte[8000],
                                              metadata: new byte[] {7, 17});
            long tmp;
            tf.Write(record, out tmp);
            tf.Close();
            Assert.AreEqual(record.GetSizeWithLengthPrefix() + 137, _checkpoint.Read());
            using (var filestream = File.Open(filename, FileMode.Open, FileAccess.Read))
            {
                filestream.Seek(137 + sizeof(int), SeekOrigin.Begin);
                var reader = new BinaryReader(filestream);
                var read = LogRecord.ReadFrom(reader);
                Assert.AreEqual(record, read);
            }
        }
    }
}