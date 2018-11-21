using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;
using EventStore.Projections.Core.Messages;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.event_reader.multi_stream_reader
{
    [TestFixture]
    public class when_handling_read_completed_for_all_streams_then_pause_requested_then_eof_then_unexpected_event_number :
        TestFixtureWithExistingEvents
    {
        private MultiStreamEventReader _edp;
        private Guid _distibutionPointCorrelationId;
        private Guid _firstEventId;
        private Guid _secondEventId;
        private Guid _thirdEventId;
        private Guid _fourthEventId;

        private Guid _fifthEventId;

        private Guid _correlationId; 

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

            _distibutionPointCorrelationId = Guid.NewGuid();
            _edp = new MultiStreamEventReader(
                _ioDispatcher, _bus, _distibutionPointCorrelationId, null, 0, _abStreams, _ab12Tag, false,
                new RealTimeProvider());
            _edp.Resume();
            _firstEventId = Guid.NewGuid();
            _secondEventId = Guid.NewGuid();
            _thirdEventId = Guid.NewGuid();
            _fourthEventId = Guid.NewGuid();
            _fifthEventId = Guid.NewGuid();
            var correlationId = _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Last(x => x.EventStreamId == "a").CorrelationId;
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    correlationId, "a", 100, 100, ReadStreamResult.Success, 
                    new[]
                        {
                            ResolvedEvent.ForUnresolvedEvent(
                        new EventRecord(
                            1, 50, Guid.NewGuid(), _firstEventId, 50, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type1", new byte[] {1}, new byte[] {2})),
                            ResolvedEvent.ForUnresolvedEvent(
                        new EventRecord(
                            2, 100, Guid.NewGuid(), _secondEventId, 100, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type2", new byte[] {3}, new byte[] {4}))
                        }, null, false, "", 3, 2, true, 200));
            correlationId = _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Last(x => x.EventStreamId == "b").CorrelationId;
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    correlationId, "b", 100, 100, ReadStreamResult.Success, 
                    new[]
                        {
                            ResolvedEvent.ForUnresolvedEvent(
                        new EventRecord(
                            2, 150, Guid.NewGuid(), _thirdEventId, 150, 0, "b", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type1", new byte[] {1}, new byte[] {2})),
                            ResolvedEvent.ForUnresolvedEvent(
                        new EventRecord(
                            3, 200, Guid.NewGuid(), _fourthEventId, 200, 0, "b", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type2", new byte[] {3}, new byte[] {4}))
                        }, null, false, "", 4, 3, true, 200));
            _edp.Pause();
            correlationId = _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Last(x => x.EventStreamId == "a").CorrelationId;
            _correlationId = correlationId;
        }

        [Test]
        public void should_throw_invalid_operation_because_unexpected_event_number()
        {
            Assert.Throws<InvalidOperationException>(          
            ()=>{
                _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                _correlationId, "a", 100, 100, ReadStreamResult.Success, new[]
                        {
                            ResolvedEvent.ForUnresolvedEvent(
                        new EventRecord(
                            4, 50, Guid.NewGuid(), _fifthEventId, 50, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type1", new byte[] {1}, new byte[] {2}))
                        }, null, false, "", 3, 2, true, 400));
            });
        }

    }
}
