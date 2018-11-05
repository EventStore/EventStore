using System;
using System.Threading;
using EventStore.Core.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Helpers.IODispatcherTests.ReadEventsTests
{
    [TestFixture]
    public class async_read_stream_events_forward_with_cancelled_read : with_read_io_dispatcher
    {
        private bool _hasTimedOut;
        private bool _hasRead;
        private bool _eventSet;

        [OneTimeSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            var mre = new ManualResetEvent(false);
            var step = _ioDispatcher.BeginReadForward(
                _cancellationScope, _eventStreamId, _fromEventNumber, _maxCount, true, _principal,
                res => { _hasRead = true; mre.Set(); },
                () => { _hasTimedOut = true; mre.Set(); }
            );
            
            IODispatcherAsync.Run(step);
            _cancellationScope.Cancel();
            _eventSet = mre.WaitOne(TimeSpan.FromSeconds(5));
        }

        [Test]
        public void should_ignore_read()
        {
            Assert.IsFalse(_eventSet);
            Assert.IsFalse(_hasRead, "Should not have completed read before replying on read message");
            _readForward.Envelope.ReplyWith(CreateReadStreamEventsForwardCompleted(_readForward));
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