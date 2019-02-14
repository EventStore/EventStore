using System;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.Services.AwakeReaderService;
using EventStore.Core.Tests.Bus.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.AwakeService {
	[TestFixture]
	public class when_handling_subscribe_awake {
		private Core.Services.AwakeReaderService.AwakeService _it;
		private Exception _exception;
		private IEnvelope _envelope;

		[SetUp]
		public void SetUp() {
			_exception = null;
			Given();
			When();
		}

		private void Given() {
			_it = new Core.Services.AwakeReaderService.AwakeService();

			_envelope = new NoopEnvelope();
		}

		private void When() {
			try {
				_it.Handle(
					new AwakeServiceMessage.SubscribeAwake(
						_envelope, Guid.NewGuid(), "Stream", new TFPos(1000, 500), new TestMessage()));
			} catch (Exception ex) {
				_exception = ex;
			}
		}

		[Test]
		public void it_is_handled() {
			Assert.IsNull(_exception, (_exception ?? (object)"").ToString());
		}
	}
}
