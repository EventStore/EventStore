using System;
using System.Threading;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Helpers.IODispatcherTests.ReadEventsTests
{
    [TestFixture]
    public class async_read_stream_events_backward_with_successful_read : with_read_io_dispatcher
    {
        private ClientMessage.ReadStreamEventsBackwardCompleted _result;
        private bool _hasTimedOut;

        [OneTimeSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            var mre = new ManualResetEvent(false);
            var step = _ioDispatcher.BeginReadBackward(
                _cancellationScope, _eventStreamId, _fromEventNumber, _maxCount, true, _principal,
                res => { _result = res; mre.Set(); },
                () => { _hasTimedOut = true; mre.Set(); }
            );
            
            IODispatcherAsync.Run(step);

            _readBackward.Envelope.ReplyWith(CreateReadStreamEventsBackwardCompleted(_readBackward));
            mre.WaitOne(TimeSpan.FromSeconds(10));
        }

        [Test]
        public void should_get_read_result()
        {
            Assert.IsNotNull(_result);
            Assert.AreEqual(_maxCount, _result.Events.Length, "Event count");
            Assert.AreEqual(_eventStreamId, _result.Events[0].OriginalStreamId, "Stream Id");
            Assert.AreEqual(_fromEventNumber, _result.Events[_maxCount - 1].OriginalEventNumber, "From event number");
        }

        [Test]
        public void should_ignore_timeout_message()
        {
            Assert.IsFalse(_hasTimedOut, "Should not have timed out before replying on timeout message");
            _timeoutMessage.Reply();
            Assert.IsFalse(_hasTimedOut);
        }
    }

    [TestFixture]
    public class read_stream_events_backward_with_successful_read: with_read_io_dispatcher
    {
        private ClientMessage.ReadStreamEventsBackwardCompleted _result;
        private bool _hasTimedOut;

        [OneTimeSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            var mre = new ManualResetEvent(false);
            _ioDispatcher.ReadBackward(
                _eventStreamId, _fromEventNumber, _maxCount, true, _principal,
                res => { _result = res; mre.Set(); },
                () => { _hasTimedOut = true; mre.Set(); },
                Guid.NewGuid()
            );

            _readBackward.Envelope.ReplyWith(CreateReadStreamEventsBackwardCompleted(_readBackward));
            mre.WaitOne(TimeSpan.FromSeconds(10));
        }

        [Test]
        public void should_get_read_result()
        {
            Assert.IsNotNull(_result);
            Assert.AreEqual(_maxCount, _result.Events.Length, "Event count");
            Assert.AreEqual(_eventStreamId, _result.Events[0].OriginalStreamId, "Stream Id");
            Assert.AreEqual(_fromEventNumber, _result.Events[_maxCount - 1].OriginalEventNumber, "From event number");
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