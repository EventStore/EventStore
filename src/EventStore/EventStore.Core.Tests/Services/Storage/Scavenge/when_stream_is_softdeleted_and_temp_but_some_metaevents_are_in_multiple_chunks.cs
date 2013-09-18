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

using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.Scavenge
{
    [TestFixture]
    public class when_stream_is_softdeleted_and_temp_but_some_metaevents_are_in_multiple_chunks : ScavengeTestScenario
    {
        protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator)
        {
            return dbCreator.Chunk(Rec.Prepare(0, "$$test", metadata: new StreamMetadata(tempStream: true)),
                                   Rec.Commit(0, "$$test"))
                            .Chunk(Rec.Prepare(1, "test"),
                                   Rec.Commit(1, "test"),
                                   Rec.Prepare(2, "test"),
                                   Rec.Commit(2, "test"),
                                   Rec.Prepare(3, "$$test", metadata: new StreamMetadata(truncateBefore: EventNumber.DeletedStream, tempStream: true)),
                                   Rec.Commit(3, "$$test"))
                            .CompleteLastChunk()
                            .CreateDb();
        }

        protected override LogRecord[][] KeptRecords(DbResult dbResult)
        {
            return new[]
            {
                    new LogRecord[0],
                    new[]
                    {
                            dbResult.Recs[1][2],
                            dbResult.Recs[1][3],
                            dbResult.Recs[1][4],
                            dbResult.Recs[1][5]
                    }
            };
        }

        [Test]
        public void scavenging_goes_as_expected()
        {
            CheckRecords();
        }

        [Test]
        public void the_stream_is_absent_logically()
        {
            Assert.AreEqual(ReadEventResult.NoStream, ReadIndex.ReadEvent("test", 0).Result);
            Assert.AreEqual(ReadStreamResult.NoStream, ReadIndex.ReadStreamEventsForward("test", 0, 100).Result);
            Assert.AreEqual(ReadStreamResult.NoStream, ReadIndex.ReadStreamEventsBackward("test", -1, 100).Result);
        }

        [Test]
        public void the_metastream_is_present_logically()
        {
            Assert.AreEqual(ReadEventResult.Success, ReadIndex.ReadEvent("$$test", -1).Result);
            Assert.AreEqual(ReadStreamResult.Success, ReadIndex.ReadStreamEventsForward("$$test", 0, 100).Result);
            Assert.AreEqual(1, ReadIndex.ReadStreamEventsForward("$$test", 0, 100).Records.Length);
            Assert.AreEqual(ReadStreamResult.Success, ReadIndex.ReadStreamEventsBackward("$$test", -1, 100).Result);
            Assert.AreEqual(1, ReadIndex.ReadStreamEventsBackward("$$test", -1, 100).Records.Length);
        }

        [Test]
        public void the_stream_is_present_physically()
        {
            var headOfTf = new TFPos(Db.Config.WriterCheckpoint.Read(), Db.Config.WriterCheckpoint.Read());
            Assert.AreEqual(1, ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 1000).Records.Count(x => x.Event.EventStreamId == "test"));
            Assert.AreEqual(1, ReadIndex.ReadAllEventsBackward(headOfTf, 1000).Records.Count(x => x.Event.EventStreamId == "test"));
        }

        [Test]
        public void the_metastream_is_present_physically()
        {
            var headOfTf = new TFPos(Db.Config.WriterCheckpoint.Read(), Db.Config.WriterCheckpoint.Read());
            Assert.AreEqual(1, ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 1000).Records.Count(x => x.Event.EventStreamId == "$$test"));
            Assert.AreEqual(1, ReadIndex.ReadAllEventsBackward(headOfTf, 1000).Records.Count(x => x.Event.EventStreamId == "$$test"));
        }
    }
}
