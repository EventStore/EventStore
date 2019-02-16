using System;
using System.Linq;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.event_reader.event_by_type_index_event_reader {
	[TestFixture]
	public class when_tf_based_read_completes_before_timeout : EventByTypeIndexEventReaderTestFixture {
		private EventByTypeIndexEventReader _eventReader;
		private Guid _distributionCorrelationId;

		protected override void Given() {
			TicksAreHandledImmediately();
		}

		private FakeTimeProvider _fakeTimeProvider;

		[SetUp]
		public new void When() {
			_distributionCorrelationId = Guid.NewGuid();
			_fakeTimeProvider = new FakeTimeProvider();
			var fromPositions = new Dictionary<string, long>();
			fromPositions.Add("$et-eventTypeOne", 0);
			fromPositions.Add("$et-eventTypeTwo", 0);
			_eventReader = new EventByTypeIndexEventReader(_bus, _distributionCorrelationId,
				null, new string[] {"eventTypeOne", "eventTypeTwo"},
				false, new TFPos(0, 0),
				fromPositions, true,
				_fakeTimeProvider,
				stopOnEof: true);

			_eventReader.Resume();

			CompleteForwardStreamRead("$et-eventTypeOne", Guid.Empty);
			CompleteForwardStreamRead("$et-eventTypeTwo", Guid.Empty);
			CompleteBackwardStreamRead("$et", Guid.Empty);

			var correlationId = CompleteForwardAllStreamRead(Guid.Empty, new[] {
				EventStore.Core.Data.ResolvedEvent.ForUnresolvedEvent(
					new EventRecord(
						1, 50, Guid.NewGuid(), Guid.NewGuid(), 50, 0, "test_stream", ExpectedVersion.Any,
						_fakeTimeProvider.Now,
						PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
						"eventTypeOne", new byte[] {1}, new byte[] {2}), 100),
				EventStore.Core.Data.ResolvedEvent.ForUnresolvedEvent(
					new EventRecord(
						2, 150, Guid.NewGuid(), Guid.NewGuid(), 150, 0, "test_stream", ExpectedVersion.Any,
						_fakeTimeProvider.Now,
						PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
						"eventTypeTwo", new byte[] {1}, new byte[] {2}), 200),
			});

			TimeoutRead("$all", correlationId);
		}

		[Test]
		public void should_deliver_events() {
			Assert.AreEqual(2,
				_consumer.HandledMessages.OfType<ReaderSubscriptionMessage.CommittedEventDistributed>().Count());
		}
	}
}
