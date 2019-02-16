using System;
using System.Threading;
using EventStore.Core.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Helpers.IODispatcherTests.ReadEventsTests {
	[TestFixture]
	public class async_read_stream_events_backward_with_timeout_on_read : with_read_io_dispatcher {
		private bool _didTimeout;
		private bool _didReceiveRead;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			var mre = new ManualResetEvent(false);
			var step = _ioDispatcher.BeginReadBackward(
				_cancellationScope, _eventStreamId, _fromEventNumber, _maxCount, true, _principal,
				res => {
					_didReceiveRead = true;
					mre.Set();
				},
				() => {
					_didTimeout = true;
					mre.Set();
				}
			);
			IODispatcherAsync.Run(step);
			Assert.IsNotNull(_timeoutMessage, "Expected TimeoutMessage to not be null");

			_timeoutMessage.Reply();
			mre.WaitOne(TimeSpan.FromSeconds(10));
		}

		[Test]
		public void should_call_timeout_handler() {
			Assert.IsTrue(_didTimeout);
		}

		[Test]
		public void should_ignore_read_complete() {
			Assert.IsFalse(_didReceiveRead, "Should not have received read completed before replying on message");
			_readBackward.Envelope.ReplyWith(CreateReadStreamEventsBackwardCompleted(_readBackward));
			Assert.IsFalse(_didReceiveRead);
		}
	}

	[TestFixture]
	public class read_stream_events_backward_with_timeout_on_read : with_read_io_dispatcher {
		private bool _didTimeout;
		private bool _didReceiveRead;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

			var mre = new ManualResetEvent(false);
			_ioDispatcher.ReadBackward(
				_eventStreamId, _fromEventNumber, _maxCount, true, _principal,
				res => {
					_didReceiveRead = true;
					mre.Set();
				},
				() => {
					_didTimeout = true;
					mre.Set();
				},
				Guid.NewGuid()
			);
			Assert.IsNotNull(_timeoutMessage, "Expected TimeoutMessage to not be null");

			_timeoutMessage.Reply();
			mre.WaitOne(TimeSpan.FromSeconds(10));
		}

		[Test]
		public void should_call_timeout_handler() {
			Assert.IsTrue(_didTimeout);
		}

		[Test]
		public void should_ignore_read_complete() {
			Assert.IsFalse(_didReceiveRead, "Should not have received read completed before replying on message");
			_readBackward.Envelope.ReplyWith(CreateReadStreamEventsBackwardCompleted(_readBackward));
			Assert.IsFalse(_didReceiveRead);
		}
	}
}
