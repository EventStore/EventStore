using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.Scavenge
{
    [TestFixture]
    public class when_stream_contains_link_to_events_and_some_of_the_original_events_have_been_removed : ScavengeTestScenario
    {
        protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator)
        {
            return dbCreator.Chunk(Rec.Prepare(0, "linkTo-Stream", eventType: SystemEventTypes.LinkTo, data: Helper.UTF8NoBom.GetBytes("0@test-stream")),
                                   Rec.Commit(0, "linkTo-Stream"),
                                   Rec.Prepare(1, "linkTo-Stream", eventType: SystemEventTypes.LinkTo, data: Helper.UTF8NoBom.GetBytes("15@test-stream")),
                                   Rec.Commit(1, "linkTo-Stream"),
                                   Rec.Prepare(2, "linkTo-Stream", eventType: SystemEventTypes.LinkTo, data: Helper.UTF8NoBom.GetBytes("1@test-stream")),
                                   Rec.Commit(2, "linkTo-Stream"),
                                   Rec.Prepare(3, "linkTo-Stream", eventType: SystemEventTypes.LinkTo, data: Helper.UTF8NoBom.GetBytes("20@test-stream")),
                                   Rec.Commit(3, "linkTo-Stream"),
                                   Rec.Prepare(4, "test-stream"),
                                   Rec.Commit(4, "test-stream"),
                                   Rec.Prepare(5, "test-stream"),
                                   Rec.Commit(5, "test-stream"))
                            .CompleteLastChunk()
                            .CreateDb();
        }

        protected override LogRecord[][] KeptRecords(DbResult dbResult)
        {
            return new[] { 
                dbResult.Recs[0].Where((x, i) => new [] {0, 1, 4, 5, 8, 9, 10, 11}.Contains(i)).ToArray()
            };
        }

        [Test]
        public void scavenging_goes_as_expected()
        {
            CheckRecords();
        }

        [Test]
        public void the_link_to_events_that_still_exist_are_present_logically()
        {
            Assert.AreEqual(ReadEventResult.Success, ReadIndex.ReadEvent("linkTo-Stream", 0).Result);
            Assert.AreEqual(ReadEventResult.Success, ReadIndex.ReadEvent("linkTo-Stream", 2).Result);
        }

        [Test]
        public void the_link_to_events_that_were_removed_are_absent_logically()
        {
            Assert.AreEqual(ReadEventResult.NotFound, ReadIndex.ReadEvent("linkTo-Stream", 1).Result);
            Assert.AreEqual(ReadEventResult.NotFound, ReadIndex.ReadEvent("linkTo-Stream", 3).Result);
        }

        [Test]
        public void the_link_to_events_that_still_exist_are_present_logically_reading_forward()
        {
            var forward = ReadIndex.ReadStreamEventsForward("linkTo-Stream", 0, 100);
            Assert.AreEqual(ReadStreamResult.Success, forward.Result);
            Assert.AreEqual(2, forward.Records.Length);
            Assert.IsTrue(forward.Records.All(x=> x.EventType == SystemEventTypes.LinkTo));
        }

        [Test]
        public void the_link_to_events_that_still_exist_are_present_logically_reading_backward()
        {
            var backward = ReadIndex.ReadStreamEventsBackward("linkTo-Stream", -1, 100);
            Assert.AreEqual(ReadStreamResult.Success, backward.Result);
            Assert.AreEqual(2, backward.Records.Length);
            Assert.IsTrue(backward.Records.All(x => x.EventType == SystemEventTypes.LinkTo));
        }

        [Test]
        public void the_link_to_events_that_still_exist_are_present_physically_reading_forward()
        {
            var forward = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 1000).Records.Where(x => x.Event.EventStreamId == "linkTo-Stream").ToList();
            Assert.AreEqual(2, forward.Count);
            Assert.IsTrue(forward.All(x => x.Event.EventType == SystemEventTypes.LinkTo));
        }

        [Test]
        public void the_link_to_events_that_still_exist_are_present_physically_reading_backcward()
        {
            var headOfTf = new TFPos(Db.Config.WriterCheckpoint.Read(), Db.Config.WriterCheckpoint.Read());
            var backward = ReadIndex.ReadAllEventsBackward(headOfTf, 1000).Records.Where(x => x.Event.EventStreamId == "linkTo-Stream").ToList();
            Assert.AreEqual(2, backward.Count);
            Assert.IsTrue(backward.All(x => x.Event.EventType == SystemEventTypes.LinkTo));
        }
    }
}