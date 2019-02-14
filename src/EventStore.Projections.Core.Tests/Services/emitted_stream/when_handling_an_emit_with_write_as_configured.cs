using System;
using System.Linq;
using EventStore.Core.Authentication;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.emitted_stream {
	[TestFixture]
	public class when_handling_an_emit_with_write_as_configured : TestFixtureWithExistingEvents {
		private EmittedStream _stream;
		private TestCheckpointManagerMessageHandler _readyHandler;
		private OpenGenericPrincipal _writeAs;

		protected override void Given() {
			AllWritesQueueUp();
			ExistingEvent("test_stream", "type", @"{""c"": 100, ""p"": 50}", "data");
		}

		[SetUp]
		public void setup() {
			_readyHandler = new TestCheckpointManagerMessageHandler();
			_writeAs = new OpenGenericPrincipal("test-user");
			_stream = new EmittedStream(
				"test_stream",
				new EmittedStream.WriterConfiguration(new EmittedStreamsWriter(_ioDispatcher),
					new EmittedStream.WriterConfiguration.StreamMetadata(), _writeAs, maxWriteBatchLength: 50),
				new ProjectionVersion(1, 0, 0), new TransactionFilePositionTagger(0),
				CheckpointTag.FromPosition(0, 40, 30),
				_bus, _ioDispatcher, _readyHandler);
			_stream.Start();

			_stream.EmitEvents(
				new[] {
					new EmittedDataEvent(
						"test_stream", Guid.NewGuid(), "type", true, "data", null,
						CheckpointTag.FromPosition(0, 200, 150), null)
				});
		}

		[Test]
		public void publishes_not_yet_published_events() {
			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count());
		}

		[Test]
		public void publishes_write_event_with_correct_user_account() {
			var writeEvent = _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Single();

			Assert.AreSame(_writeAs, writeEvent.User);
		}
	}
}
