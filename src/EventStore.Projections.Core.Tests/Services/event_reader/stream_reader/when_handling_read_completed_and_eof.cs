using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.AwakeReaderService;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.Data;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.event_reader.stream_reader {
	[TestFixture]
	public class when_handling_read_completed_and_eof : TestFixtureWithExistingEvents {
		private StreamEventReader _edp;
		private Guid _distibutionPointCorrelationId;
		private Guid _firstEventId;
		private Guid _secondEventId;

		protected override void Given() {
			TicksAreHandledImmediately();
		}

		[SetUp]
		public new void When() {
			_distibutionPointCorrelationId = Guid.NewGuid();
			_edp = new StreamEventReader(_bus, _distibutionPointCorrelationId, null, "stream", 10,
				new RealTimeProvider(), false,
				produceStreamDeletes: false);
			_edp.Resume();
			_firstEventId = Guid.NewGuid();
			_secondEventId = Guid.NewGuid();
			var correlationId = _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Last()
				.CorrelationId;
			_edp.Handle(
				new ClientMessage.ReadStreamEventsForwardCompleted(
					correlationId, "stream", 100, 100, ReadStreamResult.Success,
					new[] {
						ResolvedEvent.ForUnresolvedEvent(
							new EventRecord(
								10, 50, Guid.NewGuid(), _firstEventId, 50, 0, "stream", ExpectedVersion.Any,
								DateTime.UtcNow,
								PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
								"event_type1", new byte[] {1}, new byte[] {2})),
						ResolvedEvent.ForUnresolvedEvent(
							new EventRecord(
								11, 100, Guid.NewGuid(), _secondEventId, 100, 0, "stream", ExpectedVersion.Any,
								DateTime.UtcNow,
								PrepareFlags.SingleWrite | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
								"event_type2", new byte[] {3}, new byte[] {4}))
					}, null, false, "", 12, 11, true, 200));
			correlationId = _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>().Last()
				.CorrelationId;
			_edp.Handle(
				new ClientMessage.ReadStreamEventsForwardCompleted(
					correlationId, "stream", 100, 100, ReadStreamResult.Success, new ResolvedEvent[0]
					, null, false, "", 12, 11, true, 400));
		}

		[Test]
		public void cannot_be_resumed() {
			Assert.Throws<InvalidOperationException>(() => { _edp.Resume(); });
		}

		[Test]
		public void cannot_be_paused() {
			_edp.Pause();
		}

		[Test]
		public void publishes_correct_committed_event_received_messages() {
			Assert.AreEqual(
				3, _consumer.HandledMessages.OfType<ReaderSubscriptionMessage.CommittedEventDistributed>().Count());
			var first =
				_consumer.HandledMessages.OfType<ReaderSubscriptionMessage.CommittedEventDistributed>().First();
			var second =
				_consumer.HandledMessages.OfType<ReaderSubscriptionMessage.CommittedEventDistributed>()
					.Skip(1)
					.First();
			var third =
				_consumer.HandledMessages.OfType<ReaderSubscriptionMessage.CommittedEventDistributed>()
					.Skip(2)
					.First();
			Assert.IsNull(third.Data);
			Assert.AreEqual(400, third.SafeTransactionFileReaderJoinPosition);

			Assert.AreEqual("event_type1", first.Data.EventType);
			Assert.AreEqual("event_type2", second.Data.EventType);
			Assert.AreEqual(_firstEventId, first.Data.EventId);
			Assert.AreEqual(_secondEventId, second.Data.EventId);
			Assert.AreEqual(1, first.Data.Data[0]);
			Assert.AreEqual(2, first.Data.Metadata[0]);
			Assert.AreEqual(3, second.Data.Data[0]);
			Assert.AreEqual(4, second.Data.Metadata[0]);
			Assert.AreEqual("stream", first.Data.EventStreamId);
			Assert.AreEqual("stream", second.Data.EventStreamId);
			Assert.AreEqual(50, first.Data.Position.PreparePosition);
			Assert.AreEqual(100, second.Data.Position.PreparePosition);
			Assert.AreEqual(-1, first.Data.Position.CommitPosition);
			Assert.AreEqual(-1, second.Data.Position.CommitPosition);
			Assert.AreEqual(50, first.Data.EventOrLinkTargetPosition.PreparePosition);
			Assert.AreEqual(100, second.Data.EventOrLinkTargetPosition.PreparePosition);
			Assert.AreEqual(-1, first.Data.EventOrLinkTargetPosition.CommitPosition);
			Assert.AreEqual(-1, second.Data.EventOrLinkTargetPosition.CommitPosition);
			Assert.AreEqual(50, first.SafeTransactionFileReaderJoinPosition);
			Assert.AreEqual(100, second.SafeTransactionFileReaderJoinPosition);
		}

		[Test]
		public void publishes_subscribe_awake() {
			Assert.AreEqual(2, _consumer.HandledMessages.OfType<AwakeServiceMessage.SubscribeAwake>().Count());
		}
	}
}
