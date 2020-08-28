using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.Data;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.event_reader.multi_stream_reader {
	[TestFixture]
	public class when_read_completes_before_timeout : TestFixtureWithExistingEvents {
		private MultiStreamEventReader _eventReader;
		private Guid _distibutionPointCorrelationId;

		protected override void Given() {
			TicksAreHandledImmediately();
		}

		private string[] _abStreams;
		private Dictionary<string, long> _ab12Tag;

		[SetUp]
		public new void When() {
			_ab12Tag = new Dictionary<string, long> {{"a", 1}, {"b", 2}};
			_abStreams = new[] {"a", "b"};

			_distibutionPointCorrelationId = Guid.NewGuid();
			_eventReader = new MultiStreamEventReader(
				_ioDispatcher, _bus, _distibutionPointCorrelationId, null, 0, _abStreams, _ab12Tag, false,
				new RealTimeProvider());
			_eventReader.Resume();
			var correlationId = _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>()
				.Last(x => x.EventStreamId == "a").CorrelationId;
			_eventReader.Handle(
				new ClientMessage.ReadStreamEventsForwardCompleted(
					correlationId, "a", 100, 100, ReadStreamResult.Success,
					new[] {
						ResolvedEvent.ForUnresolvedEvent(
							new EventRecord(
								1, 50, Guid.NewGuid(), Guid.NewGuid(), 50, 0, "a", ExpectedVersion.Any, DateTime.UtcNow,
								PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd |
								PrepareFlags.IsJson,
								"event_type1", new byte[] {1}, new byte[] {2})),
						ResolvedEvent.ForUnresolvedEvent(
							new EventRecord(
								2, 150, Guid.NewGuid(), Guid.NewGuid(), 150, 0, "a", ExpectedVersion.Any,
								DateTime.UtcNow,
								PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
								"event_type2", new byte[] {3}, new byte[] {4}))
					}, null, false, "", 3, 2, true, 200));
			_eventReader.Handle(
				new ProjectionManagementMessage.Internal.ReadTimeout(correlationId, "a"));
			correlationId = _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>()
				.Last(x => x.EventStreamId == "b").CorrelationId;
			_eventReader.Handle(
				new ClientMessage.ReadStreamEventsForwardCompleted(
					correlationId, "b", 100, 100, ReadStreamResult.Success,
					new[] {
						ResolvedEvent.ForUnresolvedEvent(
							new EventRecord(
								2, 100, Guid.NewGuid(), Guid.NewGuid(), 100, 0, "b", ExpectedVersion.Any,
								DateTime.UtcNow,
								PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
								"event_type1", new byte[] {1}, new byte[] {2})),
						ResolvedEvent.ForUnresolvedEvent(
							new EventRecord(
								3, 200, Guid.NewGuid(), Guid.NewGuid(), 200, 0, "b", ExpectedVersion.Any,
								DateTime.UtcNow,
								PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
								"event_type2", new byte[] {3}, new byte[] {4}))
					}, null, false, "", 4, 3, true, 200));
			_eventReader.Handle(
				new ProjectionManagementMessage.Internal.ReadTimeout(correlationId, "b"));
		}

		[Test]
		public void should_deliver_events() {
			Assert.AreEqual(3,
				_consumer.HandledMessages.OfType<ReaderSubscriptionMessage.CommittedEventDistributed>().Count());
		}
	}
}
