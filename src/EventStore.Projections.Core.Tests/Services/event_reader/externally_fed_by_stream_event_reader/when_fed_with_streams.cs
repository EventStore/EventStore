using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reader.externally_fed_by_stream_event_reader {
	namespace when_reading_catalog {
		abstract class with_externally_fed_reader : TestFixtureWithEventReaderService {
			protected const int TailLength = 10;
			protected Guid _subscriptionId;
			protected IReaderStrategy _readerStrategy;
			protected ReaderSubscriptionOptions _readerSubscriptionOptions;

			protected override bool GivenHeadingReaderRunning() {
				return false;
			}

			protected override void Given() {
				base.Given();
				AllWritesSucceed();
				ExistingEvent("test-stream", "type1", "{}", "{Data: 1}");
				ExistingEvent("test-stream", "type1", "{}", "{Data: 2}");
				ExistingEvent("test-stream2", "type1", "{}", "{Data: 3}");
				ExistingEvent("test-stream2", "type1", "{}", "{Data: 4}");
				ExistingEvent("test-stream3", "type1", "{}", "{Data: 5}");
				ExistingEvent("test-stream3", "type1", "{}", "{Data: 6}");
				ExistingEvent("test-stream4", "type1", "{}", "{Data: 7}");


				_subscriptionId = Guid.NewGuid();
				_readerStrategy = ReaderStrategy.CreateExternallyFedReaderStrategy(
					"test",
					0,
					_timeProvider,
					SystemAccount.Principal,
					limitingCommitPosition: 10000);

				_readerSubscriptionOptions = new ReaderSubscriptionOptions(
					checkpointUnhandledBytesThreshold: 10000, checkpointProcessedEventsThreshold: 100,
					checkpointAfterMs: 10000, stopOnEof: true,
					stopAfterNEvents: null);
			}
		}

		[TestFixture]
		class when_starting_from_the_beginning : with_externally_fed_reader {
			protected override IEnumerable<WhenStep> When() {
				var fromZeroPosition = CheckpointTag.FromByStreamPosition(0, "", -1, null, -1, 1000);
				yield return
					new ReaderSubscriptionManagement.Subscribe(
						_subscriptionId, fromZeroPosition, _readerStrategy, _readerSubscriptionOptions);
			}

			[Test]
			public void publishes_reader_idle_message() {
				Assert.IsNotEmpty(HandledMessages.OfType<ReaderSubscriptionMessage.EventReaderIdle>());
			}
		}


		[TestFixture]
		class when_handling_first_spool_stream_reading_message : with_externally_fed_reader {
			protected override IEnumerable<WhenStep> When() {
				var fromZeroPosition = CheckpointTag.FromByStreamPosition(0, "", -1, null, -1, 10000);
				yield return
					new ReaderSubscriptionManagement.Subscribe(
						_subscriptionId, fromZeroPosition, _readerStrategy, _readerSubscriptionOptions);
				yield return
					new ReaderSubscriptionManagement.SpoolStreamReadingCore(_subscriptionId,
						"test-stream",
						0,
						10000);
			}

			[Test]
			public void publishes_all_events_from_the_stream() {
				var events = HandledMessages.OfType<EventReaderSubscriptionMessage.CommittedEventReceived>().ToArray();
				Assert.AreEqual(2, events.Length);
				Assert.That(events.All(v => v.Data.PositionStreamId == "test-stream"));
			}

			[Test]
			public void publishes_partition_eof_message_at_the_end() {
				var events = HandledMessages.OfType<EventReaderSubscriptionMessageBase>();
				var lastEvent = events.LastOrDefault();

				Assert.IsNotNull(lastEvent);

				Assert.IsAssignableFrom<EventReaderSubscriptionMessage.PartitionEofReached>(lastEvent);
			}
		}

		[TestFixture]
		class when_handling_sequence_of_spool_stream_reading_messages : with_externally_fed_reader {
			protected override IEnumerable<WhenStep> When() {
				var fromZeroPosition = CheckpointTag.FromByStreamPosition(0, "", -1, null, -1, 10000);
				yield return
					new ReaderSubscriptionManagement.Subscribe(
						_subscriptionId, fromZeroPosition, _readerStrategy, _readerSubscriptionOptions);
				yield return
					new WhenStep(
						new ReaderSubscriptionManagement.SpoolStreamReadingCore(_subscriptionId,
							"test-stream",
							0,
							10000),
						new ReaderSubscriptionManagement.SpoolStreamReadingCore(_subscriptionId,
							"test-stream2",
							1,
							10000));
			}

			[Test]
			public void publishes_all_events_from_the_stream() {
				var events = HandledMessages.OfType<EventReaderSubscriptionMessage.CommittedEventReceived>().ToArray();
				Assert.AreEqual(4, events.Length);
				Assert.That(
					events.Select(v => v.Data.PositionStreamId)
						.SequenceEqual(new[] {"test-stream", "test-stream", "test-stream2", "test-stream2"}));
			}

			[Test]
			public void publishes_partition_eof_message_at_the_end_of_each_stream() {
				var events =
					HandledMessages
						.OfTypes
						<EventReaderSubscriptionMessageBase, EventReaderSubscriptionMessage.PartitionEofReached,
							EventReaderSubscriptionMessage.CommittedEventReceived>().ToArray();
				Assert.IsAssignableFrom<EventReaderSubscriptionMessage.PartitionEofReached>(events[2]);
				Assert.IsAssignableFrom<EventReaderSubscriptionMessage.PartitionEofReached>(events[5]);
			}
		}

		[TestFixture]
		class when_handling_sequence_of_spool_stream_reading_messages_with_delays : with_externally_fed_reader {
			protected override IEnumerable<WhenStep> When() {
				var fromZeroPosition = CheckpointTag.FromByStreamPosition(0, "", -1, null, -1, 10000);
				yield return
					new ReaderSubscriptionManagement.Subscribe(
						_subscriptionId,
						fromZeroPosition,
						_readerStrategy,
						_readerSubscriptionOptions);
				yield return
					new ReaderSubscriptionManagement.SpoolStreamReadingCore(_subscriptionId,
						"test-stream",
						0,
						10000);
				yield return Yield;

				Assert.AreEqual(
					2,
					HandledMessages.OfType<EventReaderSubscriptionMessage.CommittedEventReceived>().Count());

				yield return
					new ReaderSubscriptionManagement.SpoolStreamReadingCore(_subscriptionId,
						"test-stream2",
						1,
						10000);
			}

			[Test]
			public void publishes_all_events_from_the_stream() {
				var events = HandledMessages.OfType<EventReaderSubscriptionMessage.CommittedEventReceived>().ToArray();
				Assert.AreEqual(4, events.Length);
				Assert.That(
					events.Select(v => v.Data.PositionStreamId)
						.SequenceEqual(new[] {"test-stream", "test-stream", "test-stream2", "test-stream2"}));
			}

			[Test]
			public void publishes_partition_eof_message_at_the_end_of_each_stream() {
				var events =
					HandledMessages
						.OfTypes
						<EventReaderSubscriptionMessageBase, EventReaderSubscriptionMessage.PartitionEofReached,
							EventReaderSubscriptionMessage.CommittedEventReceived>().ToArray();
				Assert.IsAssignableFrom<EventReaderSubscriptionMessage.PartitionEofReached>(events[2]);
				Assert.IsAssignableFrom<EventReaderSubscriptionMessage.PartitionEofReached>(events[5]);
			}
		}

		[TestFixture]
		class when_handling_sequence_of_spool_stream_reading_messages_followed_by_completed_spooled_reading :
			with_externally_fed_reader {
			protected override IEnumerable<WhenStep> When() {
				var fromZeroPosition = CheckpointTag.FromByStreamPosition(0, "", -1, null, -1, 10000);
				yield return
					new ReaderSubscriptionManagement.Subscribe(
						_subscriptionId, fromZeroPosition, _readerStrategy, _readerSubscriptionOptions);
				yield return
					new WhenStep(
						new ReaderSubscriptionManagement.SpoolStreamReadingCore(_subscriptionId,
							"test-stream",
							0,
							10000),
						new ReaderSubscriptionManagement.SpoolStreamReadingCore(_subscriptionId,
							"test-stream2",
							1,
							10000),
						new ReaderSubscriptionManagement.CompleteSpooledStreamReading(_subscriptionId));
			}

			[Test]
			public void publishes_all_events_from_the_stream() {
				var events = HandledMessages.OfType<EventReaderSubscriptionMessage.CommittedEventReceived>().ToArray();
				Assert.AreEqual(4, events.Length);
				Assert.That(
					events.Select(v => v.Data.PositionStreamId)
						.SequenceEqual(new[] {"test-stream", "test-stream", "test-stream2", "test-stream2"}));
			}

			[Test]
			public void publishes_partition_eof_message_at_the_end_of_each_stream() {
				var events =
					HandledMessages
						.OfTypes
						<EventReaderSubscriptionMessageBase, EventReaderSubscriptionMessage.PartitionEofReached,
							EventReaderSubscriptionMessage.CommittedEventReceived>().ToArray();
				Assert.IsAssignableFrom<EventReaderSubscriptionMessage.PartitionEofReached>(events[2]);
				Assert.IsAssignableFrom<EventReaderSubscriptionMessage.PartitionEofReached>(events[5]);
			}

			[Test]
			public void publishes_eof_message() {
				var lastEvent =
					HandledMessages.OfType<EventReaderSubscriptionMessageBase>()
						.LastOrDefault(v => !(v is EventReaderSubscriptionMessage.ReaderAssignedReader));
				Assert.IsNotNull(lastEvent);
				Assert.IsAssignableFrom<EventReaderSubscriptionMessage.EofReached>(lastEvent);
			}
		}
	}
}
