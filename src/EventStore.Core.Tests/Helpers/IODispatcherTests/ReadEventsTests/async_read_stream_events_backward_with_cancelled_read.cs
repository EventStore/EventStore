using EventStore.Core.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Helpers.IODispatcherTests.ReadEventsTests
{
    [TestFixture]
    public class async_read_stream_events_backward_with_cancelled_read : with_read_io_dispatcher
    {
        private bool _hasTimedOut;
        private bool _hasRead;

        [OneTimeSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            var step = _ioDispatcher.BeginReadBackward(
                _cancellationScope, _eventStreamId, _fromEventNumber, _maxCount, true, _principal,
                res => _hasRead = true,
                () => _hasTimedOut = true
            );
            
            IODispatcherAsync.Run(step);
            _cancellationScope.Cancel();
        }

        [Test]
        public void should_ignore_read()
        {
            Assert.IsFalse(_hasRead, "Should not have completed read before replying on read message");
            _readBackward.Envelope.ReplyWith(CreateReadStreamEventsBackwardCompleted(_readBackward));
            Assert.IsFalse(_hasRead);
        }

        [Test]
        public void should_ignore_timeout_message()
        {
            Assert.IsFalse(_hasTimedOut, "Should not have timed out before replying on timeout message");
            _timeoutMessage.Reply();
            Assert.IsFalse(_hasTimedOut);
        }
    }
}