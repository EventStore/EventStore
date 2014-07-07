using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.event_reader.multi_stream_reader
{
    [TestFixture]
    public class when_handling_read_completed : TestFixtureWithExistingEvents
    {
        private MultiStreamEventReader _edp;
        private Guid _distibutionPointCorrelationId;
        private Guid _firstEventId;
        private Guid _secondEventId;

        protected override void Given()
        {
            TicksAreHandledImmediately();
        }

        private string[] _abStreams;
        private Dictionary<string, int> _ab12Tag;

        [SetUp]
        public new void When()
        {
            _ab12Tag = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
            _abStreams = new[] { "a", "b" };

            _distibutionPointCorrelationId = Guid.NewGuid();
            _edp = new MultiStreamEventReader(
                _ioDispatcher, _bus, _distibutionPointCorrelationId, null, 0, _abStreams, _ab12Tag, false,
                new RealTimeProvider());
            _edp.Resume();
            _firstEventId = Guid.NewGuid();
            _secondEventId = Guid.NewGuid();
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a", 100, 100, ReadStreamResult.Success, 
                    new[]
                    {
                        new ResolvedEvent( 
                            new EventRecord(
                                1, 50, Guid.NewGuid(), _firstEventId, 50, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                                PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                                "event_type1", new byte[] {1}, new byte[] {2}), null),
                        new ResolvedEvent( 
                            new EventRecord(
                                2, 100, Guid.NewGuid(), _secondEventId, 100, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                                PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                                "event_type2", new byte[] {3}, new byte[] {4}), null)
                    }, null, false, "", 3, 4, false, 200));
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void cannot_be_resumed()
        {
            _edp.Resume();
        }

        [Test]
        public void cannot_be_paused()
        {
            _edp.Pause();
        }

        [Test]
        public void does_not_publish_committed_event_received_messages()
        {
            Assert.AreEqual(
                0, _consumer.HandledMessages.OfType<ReaderSubscriptionMessage.CommittedEventDistributed>().Count());
        }

        [Test]
        public void publishes_read_events_from_beginning_with_correct_next_event_number()
        {
            // do not publish new read requests until we consume already available events
            Assert.AreEqual(2, _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Count());
            Assert.AreEqual(
                1,
                _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>()
                         .Last(v => v.EventStreamId == "a")
                         .FromEventNumber);
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void cannot_handle_repeated_read_events_completed()
        {
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a", 100, 100, ReadStreamResult.Success, 
                    new[]
                    {
                        new ResolvedEvent( 
                            new EventRecord(
                        2, 50, Guid.NewGuid(), Guid.NewGuid(), 50, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                        PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                        "event_type", new byte[0], new byte[0]), null)
                    }, null, false, "", 3, 4, false, 100));
        }

    }
}
