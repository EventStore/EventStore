using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.multi_stream_event_distribution_point
{
    [TestFixture]
    public class when_handling_read_completed_for_all_streams_and_eof : TestFixtureWithExistingEvents
    {
        private MultiStreamReaderEventDistributionPoint _edp;
        private Guid _publishWithCorrelationId;
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
        private Dictionary<string, int> _ab12Tag;

        [SetUp]
        public void When()
        {
            _ab12Tag = new Dictionary<string, int> {{"a", 1}, {"b", 2}};
            _abStreams = new[] {"a", "b"};

            _publishWithCorrelationId = Guid.NewGuid();
            _distibutionPointCorrelationId = Guid.NewGuid();
            _edp = new MultiStreamReaderEventDistributionPoint(
                _bus, _distibutionPointCorrelationId, _abStreams, _ab12Tag, false);
            _edp.Resume();
            _firstEventId = Guid.NewGuid();
            _secondEventId = Guid.NewGuid();
            _thirdEventId = Guid.NewGuid();
            _fourthEventId = Guid.NewGuid();
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a",
                    new[]
                        {
                            new EventLinkPair(
                        new EventRecord(
                            1, 50, Guid.NewGuid(), _firstEventId, 50, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type1", new byte[] {1}, new byte[] {2}), null),
                            new EventLinkPair(
                        new EventRecord(
                            2, 100, Guid.NewGuid(), _secondEventId, 100, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type2", new byte[] {3}, new byte[] {4}), null)
                        }, RangeReadResult.Success, 3, 200, 2));
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "b",
                    new[]
                        {
                            new EventLinkPair(
                        new EventRecord(
                            2, 150, Guid.NewGuid(), _thirdEventId, 150, 0, "b", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type1", new byte[] {1}, new byte[] {2}), null),
                            new EventLinkPair(
                        new EventRecord(
                            3, 200, Guid.NewGuid(), _fourthEventId, 200, 0, "b", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type2", new byte[] {3}, new byte[] {4}), null)
                        }, RangeReadResult.Success, 12, 200, 3));
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a",
                    new EventLinkPair[]{}, RangeReadResult.Success, 3, 400, 2));
        }

        [Test]
        public void publishes_correct_committed_event_received_messages()
        {
            Assert.AreEqual(
                5, _consumer.HandledMessages.OfType<ProjectionMessage.Projections.CommittedEventDistributed>().Count());
            var first =
                _consumer.HandledMessages.OfType<ProjectionMessage.Projections.CommittedEventDistributed>().First();
            var fifth =
                _consumer.HandledMessages.OfType<ProjectionMessage.Projections.CommittedEventDistributed>()
                         .Skip(4)
                         .First();

            Assert.AreEqual("event_type1", first.Data.EventType);
            Assert.IsNull(fifth.Data);
            Assert.AreEqual(_firstEventId, first.Data.EventId);
            Assert.AreEqual(1, first.Data.Data[0]);
            Assert.AreEqual(2, first.Data.Metadata[0]);
            Assert.AreEqual("a", first.EventStreamId);
            Assert.IsNullOrEmpty("", fifth.EventStreamId);
            Assert.AreEqual(0, first.Position.PreparePosition);
            Assert.AreEqual(0, fifth.Position.PreparePosition);
            Assert.AreEqual(0, first.Position.CommitPosition);
            Assert.AreEqual(0, fifth.Position.CommitPosition);
            Assert.AreEqual(null, first.SafeTransactionFileReaderJoinPosition);
            Assert.AreEqual(200, fifth.SafeTransactionFileReaderJoinPosition);
        }

        [Test]
        public void publishes_read_events_from_beginning_with_correct_next_event_number()
        {
            Assert.AreEqual(4, _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Count());
            Assert.IsTrue(
                _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>()
                         .Any(m => m.EventStreamId == "a"));
            Assert.IsTrue(
                _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>()
                         .Any(m => m.EventStreamId == "b"));
            Assert.AreEqual(
                3,
                _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>()
                         .Last(m => m.EventStreamId == "a")
                         .FromEventNumber);
            Assert.AreEqual(
                4,
                _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>()
                         .Last(m => m.EventStreamId == "b")
                         .FromEventNumber);
        }

    }
}