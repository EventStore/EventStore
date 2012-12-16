using System;
using System.Text;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_the_state_handler_with_configured_state_stream_does_process_an_event_the_projection_should :
        TestFixtureWithCoreProjectionStarted
    {
        protected override void Given()
        {
            _configureBuilderByQuerySource = source =>
            {
                source.FromAll();
                source.AllEvents();
                source.SetStateStreamNameOption("state-stream");
                source.SetEmitStateUpdated();
            };
            NoStream("state-stream");
            NoStream("$projections-projection-checkpoint");
        }

        protected override void When()
        {
            //projection subscribes here
            _coreProjection.Handle(
                ProjectionSubscriptionMessage.CommittedEventReceived.Sample(
                    Guid.Empty, new EventPosition(120, 110), "/event_category/1", -1, false,
                    ResolvedEvent.Sample(
                        Guid.NewGuid(), "handle_this_type", false, Encoding.UTF8.GetBytes("data"),
                        Encoding.UTF8.GetBytes("metadata")), 0));
        }

        [Test]
        public void write_the_new_state_snapshot()
        {
            Assert.AreEqual(1, _writeEventHandler.HandledMessages.Count);

            var message = _writeEventHandler.HandledMessages[0];
            var data = Encoding.UTF8.GetString(message.Events[0].Data);
            Assert.AreEqual("data", data);
            Assert.AreEqual("state-stream", message.EventStreamId);

        }

        [Test]
        public void emit_a_state_updated_event()
        {
            Assert.AreEqual(1, _writeEventHandler.HandledMessages.Count);

            var @event = _writeEventHandler.HandledMessages[0].Events[0];
            Assert.AreEqual("StateUpdated", @event.EventType);
        }
    }
}