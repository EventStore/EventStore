using EventStore.ClientAPI;
using EventStore.Common.Utils;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using System;
using System.Threading;

namespace EventStore.Projections.Core.Tests.Services.emitted_streams_deleter.when_deleting
{
    [TestFixture]
    public class with_multiple_tracked_streams : SpecificationWithEmittedStreamsTrackerAndDeleter
    {
        protected Action _onDeleteStreamCompleted;
        protected ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private int _numberOfTrackedEvents = 200;
        private string _testStreamFormat = "test_stream_{0}";

        protected override void Given()
        {
            _onDeleteStreamCompleted = () =>
            {
                _resetEvent.Set();
            };
            base.Given();

            for (int i = 0; i < _numberOfTrackedEvents; i++)
            {
                _conn.AppendToStreamAsync(String.Format(_testStreamFormat, i), ExpectedVersion.Any, new EventData(Guid.NewGuid(), "type1", true, Helper.UTF8NoBom.GetBytes("data"), null));
                _emittedStreamsTracker.TrackEmittedStream(new EmittedEvent[]
                {
                    new EmittedDataEvent(
                        String.Format(_testStreamFormat, i), Guid.NewGuid(),  "type1",  true,
                        "data", null, CheckpointTag.FromPosition(0, 100, 50), null),
                });
            }

            var emittedStreamResult = _conn.ReadStreamEventsForwardAsync(_projectionNamesBuilder.GetEmittedStreamsName(), 0, _numberOfTrackedEvents, false, new EventStore.ClientAPI.SystemData.UserCredentials("admin", "changeit")).Result;
            Assert.AreEqual(_numberOfTrackedEvents, emittedStreamResult.Events.Length);
            Assert.AreEqual(SliceReadStatus.Success, emittedStreamResult.Status);
        }

        protected override void When()
        {
            _emittedStreamsDeleter.DeleteEmittedStreams(_onDeleteStreamCompleted);
            if (!_resetEvent.WaitOne(TimeSpan.FromSeconds(10)))
            {
                throw new Exception("Timed out waiting callback.");
            };
        }

        [Test]
        public void should_have_deleted_the_tracked_emitted_streams()
        {
            for(int i = 0; i < _numberOfTrackedEvents; i++)
            {
                var result = _conn.ReadStreamEventsForwardAsync(String.Format(_testStreamFormat, i), 0, 1, false, new EventStore.ClientAPI.SystemData.UserCredentials("admin", "changeit")).Result;
                Assert.AreEqual(SliceReadStatus.StreamNotFound, result.Status);
            }
        }


        [Test]
        public void should_have_deleted_the_checkpoint_stream()
        {
            var result = _conn.ReadStreamEventsForwardAsync(_projectionNamesBuilder.GetEmittedStreamsCheckpointName(), 0, 1, false, new EventStore.ClientAPI.SystemData.UserCredentials("admin", "changeit")).Result;
            Assert.AreEqual(SliceReadStatus.StreamNotFound, result.Status);
        }

        [Test]
        public void should_have_deleted_the_emitted_streams_stream()
        {
            var result = _conn.ReadStreamEventsForwardAsync(_projectionNamesBuilder.GetEmittedStreamsName(), 0, 1, false, new EventStore.ClientAPI.SystemData.UserCredentials("admin", "changeit")).Result;
            Assert.AreEqual(SliceReadStatus.StreamNotFound, result.Status);
        }
    }
}
