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
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Scavenge
{
    [TestFixture]
    public class when_having_deleted_stream_spanning_two_chunks: ReadIndexTestScenario
    {
        private long[] _survivors;
        private long[] _scavenged;

        protected override void WriteTestScenario()
        {
            long tmp;

            var r2 = LogRecord.Prepare(WriterCheckpoint.ReadNonFlushed(),
                                       Guid.NewGuid(),
                                       Guid.NewGuid(),
                                       WriterCheckpoint.ReadNonFlushed(),
                                       0,
                                       "s1",
                                       -1,
                                       PrepareFlags.Data | PrepareFlags.TransactionBegin,
                                       "event-type",
                                       new byte[3],
                                       new byte[3]);
            Assert.IsTrue(Writer.Write(r2, out tmp));

            var r4 = WritePrepare("s2", -1);
            var r5 = WriteCommit(r4.LogPosition, "s2", 0);
            var r6 = WriteDelete("s2");

            Writer.CompleteChunk();

            var r7 = LogRecord.Prepare(WriterCheckpoint.ReadNonFlushed(),
                                       Guid.NewGuid(),
                                       Guid.NewGuid(),
                                       r2.LogPosition,
                                       1,
                                       "s1",
                                       -1,
                                       PrepareFlags.Data | PrepareFlags.TransactionEnd,
                                       "event-type",
                                       new byte[3],
                                       new byte[3]);
            Assert.IsTrue(Writer.Write(r7, out tmp));
            
            var r9 = WritePrepare("s3", -1);
            var r10 = WriteCommit(r9.LogPosition, "s3", 0);
            var r11 = WriteDelete("s3");

            var r12 = WriteCommit(r2.LogPosition, "s1", 0);
            var r13 = WriteDelete("s1");

            Writer.CompleteChunk();

            _survivors = new[]
                         {
                                 r6.LogPosition,
                                 r11.LogPosition,
                                 r13.LogPosition
                         };
            _scavenged = new[]
                         {
                                 r2.LogPosition,
                                 r4.LogPosition,
                                 r5.LogPosition,
                                 r7.LogPosition,
                                 r9.LogPosition,
                                 r10.LogPosition,
                                 r12.LogPosition
                         };

            Scavenge(completeLast: false, mergeChunks: true);
        }

        [Test]
        public void stream_is_scavenged_after_merging_scavenge()
        {
            foreach (var logPos in _scavenged)
            {
                var chunk = Db.Manager.GetChunkFor(logPos);
                Assert.IsFalse(chunk.TryReadAt(logPos).Success);
            }

            foreach (var logPos in _survivors)
            {
                var chunk = Db.Manager.GetChunkFor(logPos);
                Assert.IsTrue(chunk.TryReadAt(logPos).Success);
            }
        }
    }
}
