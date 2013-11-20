using System;
using System.Linq;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.v8
{
    [TestFixture]
    public class when_running_a_v8_projection_emitting_stream_links : TestFixtureWithJsProjection
    {
        protected override void Given()
        {
            _projection = @"
                fromAll().when({$any: 
                    function(state, event) {
                    linkStreamTo('output-stream' + event.sequenceNumber, 'stream' + event.sequenceNumber);
                    return {};
                }});
            ";
        }

        [Test, Category("v8")]
        public void process_event_returns_true()
        {
            string state;
            EmittedEventEnvelope[] emittedEvents;
            var result = _stateHandler.ProcessEvent(
                "", CheckpointTag.FromPosition(0, 20, 10), "stream1", "type1", "category", Guid.NewGuid(), 0, "metadata",
                @"{""a"":""b""}", out state, out emittedEvents);

            Assert.IsTrue(result);
        }

        [Test, Category("v8")]
        public void process_event_returns_emitted_event()
        {
            string state;
            EmittedEventEnvelope[] emittedEvents;
            _stateHandler.ProcessEvent(
                "", CheckpointTag.FromPosition(0, 20, 10), "stream1", "type1", "category", Guid.NewGuid(), 0, "metadata",
                @"{""a"":""b""}", out state, out emittedEvents);

            Assert.IsNotNull(emittedEvents);
            Assert.AreEqual(1, emittedEvents.Length);
            Assert.AreEqual("$@", emittedEvents[0].Event.EventType);
            Assert.AreEqual("output-stream0", emittedEvents[0].Event.StreamId);
            Assert.AreEqual("stream0", emittedEvents[0].Event.Data);
        }

    }
}
