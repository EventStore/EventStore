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
using System.IO;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_opening_chunked_transaction_file_db_without_previous_files : SpecificationWithDirectory
    {
        [Test]
        public void with_a_writer_checksum_of_zero_the_first_chunk_is_created_with_correct_name()
        {
            var config = new TFChunkDbConfig(PathName,
                                             new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                             10000,
                                             0,
                                             new InMemoryCheckpoint(0),
                                             new InMemoryCheckpoint(0),
                                             new InMemoryCheckpoint(-1),
                                             new InMemoryCheckpoint(-1));
            var db = new TFChunkDb(config);
            db.Open();
            db.Dispose();

            Assert.AreEqual(1, Directory.GetFiles(PathName).Length);
            Assert.IsTrue(File.Exists(GetFilePathFor("chunk-000000.000000")));
            var fileInfo = new FileInfo(GetFilePathFor("chunk-000000.000000"));
            Assert.AreEqual(10000 + ChunkHeader.Size + ChunkFooter.Size, fileInfo.Length);
        }
    }
}