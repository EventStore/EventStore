using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reader.multi_stream_reader
{
    [TestFixture]
    public class when_handling_read_completed_for_all_streams_after_pause_requested : TestFixtureWithExistingEvents
    {
        private MultiStreamEventReader _edp;
        //private Guid _publishWithCorrelationId;
        private Guid _distibutionPointCorrelationId;
        private Guid _firstEventId;
        private Guid _secondEventId;
        private Guid _thirdEventId;
        private Guid _fourthEventId;

        protected override void Given()
        {
            TicksAreHandledImmediately();
        }

        private string[] _abStreams;
        private Dictionary<string, long> _ab12Tag;

        [SetUp]
        public new void When()
        {
            _ab12Tag = new Dictionary<string, long> {{"a", 1}, {"b", 2}};
            _abStreams = new[] {"a", "b"};

            //_publishWithCorrelationId = Guid.NewGuid();
            _distibutionPointCorrelationId = Guid.NewGuid();
            _edp = new MultiStreamEventReader(
                _ioDispatcher, _bus, _distibutionPointCorrelationId, null, 0, _abStreams, _ab12Tag, false,
                new RealTimeProvider());
            _edp.Resume();
            _firstEventId = Guid.NewGuid();
            _secondEventId = Guid.NewGuid();
            _thirdEventId = Guid.NewGuid();
            _fourthEventId = Guid.NewGuid();
            _edp.Pause();
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a", 100, 100, ReadStreamResult.Success,
                    new[]
                    {
                        EventStore.Core.Data.ResolvedEvent.ForUnresolvedEvent(
                            new EventRecord(
                                1, 50, Guid.NewGuid(), _firstEventId, 50, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                                PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                                "event_type1", new byte[] {1}, new byte[] {2})),
                        EventStore.Core.Data.ResolvedEvent.ForUnresolvedEvent(
                            new EventRecord(
                                2, 150, Guid.NewGuid(), _secondEventId, 150, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                                PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                                "event_type2", new byte[] {3}, new byte[] {4}))
                        }, null, false, "", 3, 2, true, 200));
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "b", 100, 100, ReadStreamResult.Success, 
                    new[]
                    {
                        EventStore.Core.Data.ResolvedEvent.ForUnresolvedEvent(
                            new EventRecord(
                                2, 100, Guid.NewGuid(), _thirdEventId, 100, 0, "b", ExpectedVersion.Any, DateTime.UtcNow,
                                PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                                "event_type1", new byte[] {1}, new byte[] {2})),
                        EventStore.Core.Data.ResolvedEvent.ForUnresolvedEvent(
                            new EventRecord(
                                3, 200, Guid.NewGuid(), _fourthEventId, 200, 0, "b", ExpectedVersion.Any, DateTime.UtcNow,
                                PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                                "event_type2", new byte[] {3}, new byte[] {4}))
                    }, null, false, "", 4, 3, true, 200));
        }

        [Test]
        public void can_be_resumed()
        {
            _edp.Resume();
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void cannot_be_paused()
        {
            _edp.Pause();
        }

        [Test]
        public void publishes_correct_number_of_committed_event_received_messages()
        {
            Assert.AreEqual(
                3, _consumer.HandledMessages.OfType<ReaderSubscriptionMessage.CommittedEventDistributed>().Count());
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void cannot_handle_following_read_events_completed() 
        {
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a", 100, 100, ReadStreamResult.Success,
                    new[]
                    {
                        EventStore.Core.Data.ResolvedEvent.ForUnresolvedEvent(
                            new EventRecord(
                                3, 250, Guid.NewGuid(), Guid.NewGuid(), 250, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                                PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                                "event_type", new byte[0], new byte[0]))
                    }, null, false, "", 4, 4, false, 300));
        }

    }
}
