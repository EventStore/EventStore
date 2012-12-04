using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reader.multi_stream_reader
{
    [TestFixture]
    public class when_onetime_reader_handles_eof : TestFixtureWithExistingEvents
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
        private FakeTimeProvider _fakeTimeProvider;

        [SetUp]
        public void When()
        {
            _ab12Tag = new Dictionary<string, int> {{"a", 1}, {"b", 2}};
            _abStreams = new[] {"a", "b"};

            _distibutionPointCorrelationId = Guid.NewGuid();
            _fakeTimeProvider = new FakeTimeProvider();
            _edp = new MultiStreamEventReader(
                _bus, _distibutionPointCorrelationId, _abStreams, _ab12Tag, false, _fakeTimeProvider, stopOnEof: true);
            _edp.Resume();
            _firstEventId = Guid.NewGuid();
            _secondEventId = Guid.NewGuid();
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a",
                    new[]
                        {
                            new EventLinkPair(
                        new EventRecord(
                            1, 50, Guid.NewGuid(), _firstEventId, 50, 0, "a", ExpectedVersion.Any, _fakeTimeProvider.Now,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type1", new byte[] {1}, new byte[] {2}), null),
                        }, RangeReadResult.Success, 2, 1, true,
                    200));
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "b",
                    new[]
                        {
                            new EventLinkPair(
                        new EventRecord(
                            2, 100, Guid.NewGuid(), _secondEventId, 100, 0, "b", ExpectedVersion.Any, _fakeTimeProvider.Now,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type1", new byte[] {1}, new byte[] {2}), null),
                        }, RangeReadResult.Success, 3, 2, true,
                    200));
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "a", new EventLinkPair[] {}, RangeReadResult.Success, 2, 1, true,
                    400));
            _edp.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
                    _distibutionPointCorrelationId, "b", new EventLinkPair[] {}, RangeReadResult.Success, 3, 2, true,
                    400));
        }

        [Test]
        public void publishes_eof_message()
        {
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionCoreServiceMessage.EventReaderEof>().Count());
            var first = _consumer.HandledMessages.OfType<ProjectionCoreServiceMessage.EventReaderEof>().First();
            Assert.AreEqual(first.CorrelationId, _distibutionPointCorrelationId);
        }

        [Test]
        public void does_not_publish_read_messages_anymore()
        {
            Assert.AreEqual(4, _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Count());
        }
    }
}