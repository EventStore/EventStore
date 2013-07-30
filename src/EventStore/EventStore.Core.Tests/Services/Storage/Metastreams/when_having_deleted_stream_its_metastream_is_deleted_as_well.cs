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

using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.Metastreams
{
    [TestFixture]
    public class when_having_deleted_stream_its_metastream_is_deleted_as_well: SimpleDbTestScenario
    {
        protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator)
        {
            return dbCreator.Chunk(Rec.Prepare(0, "test"),
                                   Rec.Commit(0, "test"),
                                   Rec.Prepare(1, "$$test", metadata: new StreamMetadata(2, null, null, null, null)),
                                   Rec.Commit(1, "$$test"),
                                   Rec.Delete(2, "test"),
                                   Rec.Commit(2, "test"))
                            .CreateDb();
        }

        [Test]
        public void the_stream_is_deleted()
        {
            Assert.IsTrue(ReadIndex.IsStreamDeleted("test"));
        }

        [Test]
        public void the_metastream_is_deleted()
        {
            Assert.IsTrue(ReadIndex.IsStreamDeleted("$$test"));
        }

        [Test]
        public void get_last_event_number_reports_deleted_metastream()
        {
            Assert.AreEqual(EventNumber.DeletedStream, ReadIndex.GetLastStreamEventNumber("$$test"));
        }

        [Test]
        public void single_event_read_reports_deleted_metastream()
        {
            Assert.AreEqual(ReadEventResult.StreamDeleted, ReadIndex.ReadEvent("$$test", 0).Result);
        }

        [Test]
        public void last_event_read_reports_deleted_metastream()
        {
            Assert.AreEqual(ReadEventResult.StreamDeleted, ReadIndex.ReadEvent("$$test", -1).Result);
        }

        [Test]
        public void read_stream_events_forward_reports_deleted_metastream()
        {
            Assert.AreEqual(ReadStreamResult.StreamDeleted, ReadIndex.ReadStreamEventsForward("$$test", 0, 100).Result);
        }

        [Test]
        public void read_stream_events_backward_reports_deleted_metastream()
        {
            Assert.AreEqual(ReadStreamResult.StreamDeleted, ReadIndex.ReadStreamEventsBackward("$$test", 0, 100).Result);
        }
    }
}