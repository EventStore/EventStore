using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using System;

namespace EventStore.Projections.Core.Tests.Services.emitted_streams_tracker.when_tracking
{
    public class with_tracking_disabled : SpecificationWithEmittedStreamsTrackerAndDeleter
    {
        protected override void Given()
        {
            _trackEmittedStreams = false;
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
            Assert.AreEqual(0, result.Events.Length);
        }
    }
}
