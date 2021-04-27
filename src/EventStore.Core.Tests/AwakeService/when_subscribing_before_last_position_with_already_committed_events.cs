using System;
using System.Linq;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.AwakeReaderService;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.AwakeService {
	[TestFixture]
	public class when_subscribing_before_last_position_with_already_committed_events {
		private Core.Services.AwakeReaderService.AwakeService _it;
		private EventRecord _eventRecord;
		private StorageMessage.EventCommitted _eventCommitted;
		private Exception _exception;
		private IEnvelope _envelope;
		private InMemoryBus _publisher;
		private TestHandler<TestMessage> _handler;
		private TestMessage _reply1;
		private TestMessage _reply2;
		private TestMessage _reply3;

		[SetUp]
		public void SetUp() {
			_exception = null;
			Given();
			When();
		}

		private class TestMessage : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly int Kind;

			public TestMessage(int kind) {
				Kind = kind;
			}
		}

		private void Given() {
			_it = new Core.Services.AwakeReaderService.AwakeService();

			_eventRecord = new EventRecord(
				100,
				new PrepareLogRecord(
					1500, Guid.NewGuid(), Guid.NewGuid(), 1500, 0, "Stream", 99, DateTime.UtcNow, PrepareFlags.Data,
					"event", new byte[0], null),"Stream");
			_eventCommitted = new StorageMessage.EventCommitted(2000, _eventRecord, isTfEof: true);
			_publisher = new InMemoryBus("bus");
			_envelope = new PublishEnvelope(_publisher);
			_handler = new TestHandler<TestMessage>();
			_publisher.Subscribe(_handler);
			_reply1 = new TestMessage(1);
			_reply2 = new TestMessage(3);
			_reply3 = new TestMessage(4);

			_it.Handle(_eventCommitted);
		}

		private void When() {
			try {
				_it.Handle(
					new AwakeServiceMessage.SubscribeAwake(
						_envelope, Guid.NewGuid(), "Stream", new TFPos(1000, 500), _reply1));
				_it.Handle(
					new AwakeServiceMessage.SubscribeAwake(
						_envelope, Guid.NewGuid(), "Stream2", new TFPos(1000, 500), _reply2));
				_it.Handle(
					new AwakeServiceMessage.SubscribeAwake(
						_envelope, Guid.NewGuid(), null, new TFPos(1000, 500), _reply3));
			} catch (Exception ex) {
				_exception = ex;
			}
		}

		[Test]
		public void it_is_handled() {
			Assert.IsNull(_exception, (_exception ?? (object)"").ToString());
		}

		[Test]
		public void immediately_awakes_stream_subscriber() {
			Assert.That(_handler.HandledMessages.Any(m => m.Kind == 1));
		}

		[Test]
		public void immediately_awakes_all_subscriber() {
			Assert.That(_handler.HandledMessages.Any(m => m.Kind == 4));
		}

		[Test]
		public void immediately_awakes_another_stream_subscriber() {
			Assert.That(_handler.HandledMessages.Any(m => m.Kind == 3));
		}
	}
}
