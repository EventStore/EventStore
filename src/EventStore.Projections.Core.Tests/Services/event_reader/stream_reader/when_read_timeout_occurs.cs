using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.event_reader.stream_reader
{
    [TestFixture]
    public class when_read_timeout_occurs : TestFixtureWithExistingEvents
    {
        private StreamEventReader _eventReader;
        private Guid _distributionCorrelationId;
        private FakeTimeProvider _fakeTimeProvider;

        protected override void Given()
        {
            TicksAreHandledImmediately();
        }

        [SetUp]
        public new void When()
        {
            _distributionCorrelationId = Guid.NewGuid();
            _fakeTimeProvider = new FakeTimeProvider();
            _eventReader = new StreamEventReader(_bus, _distributionCorrelationId, null, "stream", 10, _fakeTimeProvider,
                resolveLinkTos: false, stopOnEof: true, produceStreamDeletes: false);
            _eventReader.Resume();
			var correlationId = _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Last().CorrelationId;
            _eventReader.Handle(
				new ProjectionManagementMessage.Internal.ReadTimeout(correlationId, "stream"));
            _eventReader.Handle(
                new ClientMessage.ReadStreamEventsForwardCompleted(
					correlationId, "stream", 100, 100, ReadStreamResult.Success, 
                    new[]
                        {
                            ResolvedEvent.ForUnresolvedEvent(
                        new EventRecord(
                            10, 50, Guid.NewGuid(), Guid.NewGuid(), 50, 0, "stream", ExpectedVersion.Any, DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type1", new byte[] {1}, new byte[] {2})),
                            ResolvedEvent.ForUnresolvedEvent(
                        new EventRecord(
                            11, 100, Guid.NewGuid(), Guid.NewGuid(), 100, 0, "stream", ExpectedVersion.Any,
                            DateTime.UtcNow,
                            PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                            "event_type2", new byte[] {3}, new byte[] {4}))
                        }, null, false, "", 12, 11, true, 200));
        }
			
        [Test]
        public void should_not_deliver_events()
        {
            Assert.AreEqual(0, _consumer.HandledMessages.OfType<ReaderSubscriptionMessage.CommittedEventDistributed>().Count());
        }
    }
}
