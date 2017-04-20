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
    public class when_stream_contains_link_to_event_that_points_to_an_event_that_still_exists : ScavengeTestScenario
    {
        protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator)
        {
            return dbCreator.Chunk(Rec.Prepare(0, "linkTo-Stream", eventType: SystemEventTypes.LinkTo, data: Helper.UTF8NoBom.GetBytes("0@test-stream")),
                                   Rec.Commit(0, "linkTo-Stream"),
                                   Rec.Prepare(1, "kept-stream"),
                                   Rec.Commit(1, "kept-stream"),
                                   Rec.Prepare(2, "test-stream"),
                                   Rec.Commit(2, "test-stream"))
                            .CompleteLastChunk()
                            .CreateDb();
        }

        protected override LogRecord[][] KeptRecords(DbResult dbResult)
        {
            return new[] { 
                dbResult.Recs[0].Where((x, i) => new [] {0, 1, 2, 3, 4, 5}.Contains(i)).ToArray()
            };
        }

        [Test]
        public void scavenging_goes_as_expected()
        {
            CheckRecords();
        }

        [Test]
        public void the_link_to_event_is_present_logically()
        {
            Assert.AreEqual(ReadEventResult.NotFound, ReadIndex.ReadEvent("linkTo-Stream", 1).Result);
        }

        [Test]
        public void the_link_to_event_is_present_logically_reading_forward()
        {
            var forward = ReadIndex.ReadStreamEventsForward("linkTo-Stream", 0, 100);
            Assert.AreEqual(ReadStreamResult.Success, forward.Result);
            Assert.AreEqual(1, forward.Records.Length);
            Assert.IsTrue(forward.Records[0].EventType == SystemEventTypes.LinkTo);
        }

        [Test]
        public void the_link_to_event_is_present_logically_reading_backward()
        {
            var backward = ReadIndex.ReadStreamEventsBackward("linkTo-Stream", -1, 100);
            Assert.AreEqual(ReadStreamResult.Success, backward.Result);
            Assert.AreEqual(1, backward.Records.Length);
            Assert.IsTrue(backward.Records[0].EventType == SystemEventTypes.LinkTo);
        }

        [Test]
        public void the_link_to_event_is_present_physically_reading_forward()
        {
            var forward = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 1000).Records.Where(x => x.Event.EventStreamId == "linkTo-Stream").ToList();
            Assert.AreEqual(1, forward.Count());
            Assert.IsTrue(forward[0].Event.EventType == SystemEventTypes.LinkTo);
        }

        [Test]
        public void the_link_to_event_is_present_physically_reading_backward()
        {
            var headOfTf = new TFPos(Db.Config.WriterCheckpoint.Read(), Db.Config.WriterCheckpoint.Read());
            var backward = ReadIndex.ReadAllEventsBackward(headOfTf, 1000).Records.Where(x => x.Event.EventStreamId == "linkTo-Stream").ToList();
            Assert.AreEqual(1, backward.Count());
            Assert.IsTrue(backward[0].Event.EventType == SystemEventTypes.LinkTo);
        }
    }
}