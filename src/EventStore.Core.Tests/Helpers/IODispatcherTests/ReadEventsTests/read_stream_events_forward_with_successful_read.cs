using System;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Helpers.IODispatcherTests.ReadEventsTests
{
    [TestFixture]
    public class async_read_stream_events_forward_with_successful_read : with_read_io_dispatcher
    {
        private ClientMessage.ReadStreamEventsForwardCompleted _result;
        private bool _hasTimedOut;

        [OneTimeSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            var step = _ioDispatcher.BeginReadForward(
                _cancellationScope, _eventStreamId, _fromEventNumber, _maxCount, true, _principal,
                res => _result = res,
                () => _hasTimedOut = true
            );
            
            IODispatcherAsync.Run(step);

            _readForward.Envelope.ReplyWith(CreateReadStreamEventsForwardCompleted(_readForward));
        }

        [Test]
        public void should_get_read_result()
        {
            Assert.IsNotNull(_result);
            Assert.AreEqual(_maxCount, _result.Events.Length, "Event count");
            Assert.AreEqual(_eventStreamId, _result.Events[0].OriginalStreamId, "Stream Id");
            Assert.AreEqual(_fromEventNumber, _result.Events[0].OriginalEventNumber, "From event number");
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
    public class read_stream_events_forward_with_successful_read : with_read_io_dispatcher
    {
        private ClientMessage.ReadStreamEventsForwardCompleted _result;
        private bool _hasTimedOut;

        [OneTimeSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            _ioDispatcher.ReadForward(
                _eventStreamId, _fromEventNumber, _maxCount, true, _principal,
                res => _result = res,
                () => _hasTimedOut = true,
                Guid.NewGuid()
            );
            
            _readForward.Envelope.ReplyWith(CreateReadStreamEventsForwardCompleted(_readForward));
        }

        [Test]
        public void should_get_read_result()
        {
            Assert.IsNotNull(_result);
            Assert.AreEqual(_maxCount, _result.Events.Length, "Event count");
            Assert.AreEqual(_eventStreamId, _result.Events[0].OriginalStreamId, "Stream Id");
            Assert.AreEqual(_fromEventNumber, _result.Events[0].OriginalEventNumber, "From event number");
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