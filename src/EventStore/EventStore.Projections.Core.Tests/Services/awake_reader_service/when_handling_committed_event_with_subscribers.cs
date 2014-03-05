// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 


using System;
using System.Linq;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.AwakeReaderService;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.awake_reader_service
{
    [TestFixture]
    public class when_handling_committed_event_with_subscribers
    {
        private AwakeReaderService _it;
        private EventRecord _eventRecord;
        private StorageMessage.EventCommitted _eventCommitted;
        private Exception _exception;
        private IEnvelope _envelope;
        private InMemoryBus _publisher;
        private TestHandler<TestMessage> _handler;
        private TestMessage _reply1;
        private TestMessage _reply2;
        private TestMessage _reply3;
        private TestMessage _reply4;
        private TestMessage _reply5;

        [SetUp]
        public void SetUp()
        {
            _exception = null;
            Given();
            When();
        }

        private class TestMessage : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            public readonly int Kind;

            public TestMessage(int kind)
            {
                Kind = kind;
            }
        }

        private void Given()
        {
            _it = new AwakeReaderService();

            _eventRecord = new EventRecord(
                100,
                new PrepareLogRecord(
                    1500, Guid.NewGuid(), Guid.NewGuid(), 1500, 0, "Stream", 99, DateTime.UtcNow, PrepareFlags.Data,
                    "event", new byte[0], null));
            _eventCommitted = new StorageMessage.EventCommitted(2000, _eventRecord);
            _publisher = new InMemoryBus("bus");
            _envelope = new PublishEnvelope(_publisher);
            _handler = new TestHandler<TestMessage>();
            _publisher.Subscribe(_handler);
            _reply1 = new TestMessage(1);
            _reply2 = new TestMessage(2);
            _reply3 = new TestMessage(3);
            _reply4 = new TestMessage(4);
            _reply5 = new TestMessage(5);

            _it.Handle(
                new AwakeReaderServiceMessage.SubscribeAwake(
                    _envelope, Guid.NewGuid(), "Stream", new TFPos(1000, 500), _reply1));
            _it.Handle(
                new AwakeReaderServiceMessage.SubscribeAwake(
                    _envelope, Guid.NewGuid(), "Stream", new TFPos(100000, 99500), _reply2));
            _it.Handle(
                new AwakeReaderServiceMessage.SubscribeAwake(
                    _envelope, Guid.NewGuid(), "Stream2", new TFPos(1000, 500), _reply3));
            _it.Handle(
                new AwakeReaderServiceMessage.SubscribeAwake(
                    _envelope, Guid.NewGuid(), null, new TFPos(1000, 500), _reply4));
            _it.Handle(
                new AwakeReaderServiceMessage.SubscribeAwake(
                    _envelope, Guid.NewGuid(), null, new TFPos(100000, 99500), _reply5));
        }

        private void When()
        {
            try
            {
                _it.Handle(_eventCommitted);
            }
            catch (Exception ex)
            {
                _exception = ex;
            }
        }

        [Test]
        public void it_is_handled()
        {
            Assert.IsNull(_exception, (_exception ?? (object)"").ToString());
        }

        [Test]
        public void awakes_stream_subscriber_before_position()
        {
            Assert.That(_handler.HandledMessages.Any(m => m.Kind == 1));
        }

        [Test]
        public void does_not_awake_stream_subscriber_after_position()
        {
            Assert.That(_handler.HandledMessages.All(m => m.Kind != 2));
        }

        [Test]
        public void awakes_all_subscriber_before_position()
        {
            Assert.That(_handler.HandledMessages.Any(m => m.Kind == 4));
        }

        [Test]
        public void does_not_awake_all_subscriber_after_position()
        {
            Assert.That(_handler.HandledMessages.All(m => m.Kind != 5));
        }

        [Test]
        public void does_not_awake_another_stream_subscriber_before_position()
        {
            Assert.That(_handler.HandledMessages.All(m => m.Kind != 3));
        }
    }
}