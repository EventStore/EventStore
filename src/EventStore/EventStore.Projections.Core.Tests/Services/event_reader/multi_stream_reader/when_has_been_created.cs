using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.event_reader.multi_stream_reader
{
    [TestFixture]
    public class when_has_been_created : TestFixtureWithExistingEvents
    {
        private MultiStreamEventReader _edp;
        private Guid _publishWithCorrelationId;
        private Guid _distibutionPointCorrelationId;
        private string[] _abStreams;
        private Dictionary<string, int> _ab12Tag;

        [SetUp]
        public new void When()
        {
            _ab12Tag = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
            _abStreams = new[] { "a", "b" };

            _publishWithCorrelationId = Guid.NewGuid();
            _distibutionPointCorrelationId = Guid.NewGuid();
            _edp = new MultiStreamEventReader(
                _ioDispatcher, _bus, _distibutionPointCorrelationId, null, 0, _abStreams, _ab12Tag, false,
                new RealTimeProvider());
        }

        [Test]
        public void it_can_be_resumed()
        {
            _edp.Resume();
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void it_cannot_be_paused()
        {
            _edp.Pause();
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void handle_read_events_completed_throws()
        {
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a", 100, 100, ReadStreamResult.Success, new ResolvedEvent[0], null, false, "", -1, 4, true, 100));
        }
    }
}
