﻿// Copyright (c) 2012, Event Store LLP
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
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.Metastreams
{
    [TestFixture]
    public class when_having_multiple_metaevents_in_metastream_and_read_index_is_set_to_keep_just_last: SimpleDbTestScenario
    {
        protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator)
        {
            return dbCreator.Chunk(Rec.Prepare(0, "$$test", "0", metadata: new StreamMetadata(maxCount: 10)),
                                   Rec.Prepare(0, "$$test", "1", metadata: new StreamMetadata(maxCount: 9)),
                                   Rec.Prepare(0, "$$test", "2", metadata: new StreamMetadata(maxCount: 8)),
                                   Rec.Prepare(0, "$$test", "3", metadata: new StreamMetadata(maxCount: 7)),
                                   Rec.Prepare(0, "$$test", "4", metadata: new StreamMetadata(maxCount: 6)),
                                   Rec.Commit(0, "$$test"))
                            .CreateDb();
        }

        [Test]
        public void last_event_read_returns_correct_event()
        {
            var res = ReadIndex.ReadEvent("$$test", -1);
            Assert.AreEqual(ReadEventResult.Success, res.Result);
            Assert.AreEqual("4", res.Record.EventType);
        }

        [Test]
        public void last_event_stream_number_is_correct()
        {
            Assert.AreEqual(4, ReadIndex.GetStreamLastEventNumber("$$test"));
        }

        [Test]
        public void single_event_read_returns_only_last_event()
        {
            Assert.AreEqual(ReadEventResult.NotFound, ReadIndex.ReadEvent("$$test", 0).Result);
            Assert.AreEqual(ReadEventResult.NotFound, ReadIndex.ReadEvent("$$test", 1).Result);
            Assert.AreEqual(ReadEventResult.NotFound, ReadIndex.ReadEvent("$$test", 2).Result);
            Assert.AreEqual(ReadEventResult.NotFound, ReadIndex.ReadEvent("$$test", 3).Result);

            var res = ReadIndex.ReadEvent("$$test", 4);
            Assert.AreEqual(ReadEventResult.Success, res.Result);
            Assert.AreEqual("4", res.Record.EventType);
        }

        [Test]
        public void stream_read_forward_returns_only_last_event()
        {
            var res = ReadIndex.ReadStreamEventsForward("$$test", 0, 100);
            Assert.AreEqual(ReadStreamResult.Success, res.Result);
            Assert.AreEqual(1, res.Records.Length);
            Assert.AreEqual("4", res.Records[0].EventType);
        }

        [Test]
        public void stream_read_backward_returns_only_last_event()
        {
            var res = ReadIndex.ReadStreamEventsBackward("$$test", -1, 100);
            Assert.AreEqual(ReadStreamResult.Success, res.Result);
            Assert.AreEqual(1, res.Records.Length);
            Assert.AreEqual("4", res.Records[0].EventType);
        }

        [Test]
        public void metastream_metadata_is_correct()
        {
            var metadata = ReadIndex.GetStreamMetadata("$$test");
            Assert.AreEqual(1, metadata.MaxCount);
            Assert.AreEqual(null, metadata.MaxAge);
        }

        [Test]
        public void original_stream_metadata_is_taken_from_last_metaevent()
        {
            var metadata = ReadIndex.GetStreamMetadata("test");
            Assert.AreEqual(6, metadata.MaxCount);
            Assert.AreEqual(null, metadata.MaxAge);
        }
    }
}
