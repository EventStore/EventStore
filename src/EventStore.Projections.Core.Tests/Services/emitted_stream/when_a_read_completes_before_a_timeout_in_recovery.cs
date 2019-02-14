using System;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;
using EventStore.Projections.Core.Messages;
using EventStore.Core.Services.TimerService;

namespace EventStore.Projections.Core.Tests.Services.emitted_stream {
	[TestFixture]
	public class when_a_read_completes_before_a_timeout_in_recovery : TestFixtureWithExistingEvents {
		private const string TestStreamId = "test_stream";
		private EmittedStream _stream;
		private TestCheckpointManagerMessageHandler _readyHandler;

		protected override void Given() {
			AllWritesQueueUp();
			ExistingEvent(TestStreamId, "type", @"{""c"": 100, ""p"": 50}", "data");
			ReadsBackwardQueuesUp();
		}

		[SetUp]
		public void setup() {
			_readyHandler = new TestCheckpointManagerMessageHandler();
			_stream = new EmittedStream(
				TestStreamId,
				new EmittedStream.WriterConfiguration(new EmittedStreamsWriter(_ioDispatcher),
					new EmittedStream.WriterConfiguration.StreamMetadata(), null, maxWriteBatchLength: 50),
				new ProjectionVersion(1, 0, 0), new TransactionFilePositionTagger(0),
				CheckpointTag.FromPosition(0, 40, 30),
				_bus, _ioDispatcher, _readyHandler);
			_stream.Start();
			_stream.EmitEvents(
				new[] {
					new EmittedDataEvent(
						TestStreamId, Guid.NewGuid(), "type", true, "data", null,
						CheckpointTag.FromPosition(0, 200, 150), null)
				});
			CompleteOneReadBackwards();
		}

		[Test]
		public void should_not_retry_the_read_upon_the_read_timing_out() {
			var scheduledReadTimeout =
				_consumer.HandledMessages.OfType<TimerMessage.Schedule>().First().ReplyMessage as
					ProjectionManagementMessage.Internal.ReadTimeout;
			_stream.Handle(scheduledReadTimeout);

			var readEventsBackwards = _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsBackward>()
				.Where(x => x.EventStreamId == TestStreamId);

			Assert.AreEqual(1, readEventsBackwards.Count());
		}
	}
}
