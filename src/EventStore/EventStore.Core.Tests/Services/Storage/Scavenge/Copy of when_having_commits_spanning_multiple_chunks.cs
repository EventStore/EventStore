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
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Scavenge
{
    [TestFixture]
    public class when_having_commit_spanning_multiple_chunks: ReadIndexTestScenario
    {
        private List<LogRecord> _survivors;
        private List<LogRecord> _scavenged;

        protected override void WriteTestScenario()
        {
            _survivors = new List<LogRecord>();
            _scavenged = new List<LogRecord>();

            long tmp;

            var transPos = WriterCheckpoint.ReadNonFlushed();
            
            var r1 = LogRecord.StreamCreated(transPos, Guid.NewGuid(), transPos, "s1", null, isImplicit: true);
            Assert.IsTrue(Writer.Write(r1, out tmp));
            _survivors.Add(r1);

            Writer.CompleteChunk();

            for (int i = 0; i < 10; ++i)
            {
                var r = LogRecord.Prepare(WriterCheckpoint.ReadNonFlushed(),
                                          Guid.NewGuid(),
                                          Guid.NewGuid(),
                                          transPos,
                                          i+1,
                                          "s1",
                                          -1,
                                          PrepareFlags.Data | (i == 9 ? PrepareFlags.TransactionEnd : PrepareFlags.None),
                                          "event-type",
                                          new byte[3],
                                          new byte[3]);
                Assert.IsTrue(Writer.Write(r, out tmp));
                Writer.CompleteChunk();

                _scavenged.Add(r);
            }

            var r2 = WriteCommit(transPos, "s1", 0);
            _survivors.Add(r2);

            Writer.CompleteChunk();

            var r3 = WriteDeletePrepare("s1");
            _survivors.Add(r3);

            Writer.CompleteChunk();
            
            var r4 = WriteDeleteCommit(r3);
            _survivors.Add(r4);

            Writer.CompleteChunk();

            Scavenge(completeLast: false, mergeChunks: true);

            Assert.AreEqual(14, _survivors.Count + _scavenged.Count);
        }

        [Test]
        public void all_chunks_are_merged_and_scavenged()
        {
            foreach (var rec in _scavenged)
            {
                var chunk = Db.Manager.GetChunkFor(rec.Position);
                Assert.IsFalse(chunk.TryReadAt(rec.Position).Success);
            }

            foreach (var rec in _survivors)
            {
                var chunk = Db.Manager.GetChunkFor(rec.Position);
                var res = chunk.TryReadAt(rec.Position);
                Assert.IsTrue(res.Success);
                Assert.AreEqual(rec, res.LogRecord);
            }
        }
    }
}
