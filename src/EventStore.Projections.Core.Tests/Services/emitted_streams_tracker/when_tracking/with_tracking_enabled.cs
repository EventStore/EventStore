using EventStore.ClientAPI.Common.Utils;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using System;

namespace EventStore.Projections.Core.Tests.Services.emitted_streams_tracker.when_tracking
{
    [TestFixture]
    public class with_tracking_enabled : SpecificationWithEmittedStreamsTrackerAndDeleter
    {
        protected override void Given()
        {
            base.Given();
        }

        protected override void When()
        {
            _emittedStreamsTracker.TrackEmittedStream(new EmittedEvent[]
            {
                new EmittedDataEvent(
                     "test_stream", Guid.NewGuid(),  "type1",  true,
                     "data",  null, CheckpointTag.FromPosition(0, 100, 50),  null, null)
            });
        }

        [Test]
        public void should_write_a_stream_tracked_event()
        {
            var result = _conn.ReadStreamEventsForwardAsync(_projectionNamesBuilder.GetEmittedStreamsName(), 0, 200, false, new EventStore.ClientAPI.SystemData.UserCredentials("admin", "changeit")).Result;
            Assert.AreEqual(1, result.Events.Length);
            Assert.AreEqual("test_stream", Helper.UTF8NoBom.GetString(result.Events[0].Event.Data));
        }
    }
}
