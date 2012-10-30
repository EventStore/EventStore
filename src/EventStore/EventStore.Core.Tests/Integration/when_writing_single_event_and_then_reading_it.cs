using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using EventStore.Common.Settings;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.Integration
{
    [TestFixture, Ignore("freezed because of low priority")]
    public class when_writing_single_event_and_then_reading_it :IntegrationTestBase
    {
        private EnvelopeCallback<ClientMessage.ReadEventCompleted> _readCallback;
        private EnvelopeCallback<ClientMessage.WriteEventsCompleted> _writeCallback;

        private Guid _eventId;
        private byte[] _eventData;
        private byte[] _eventMetadata;
        private string _eventStreamId;
        private string _eventType;
        private int _expectedVersion;

        protected override void SetUp()
        {
            base.SetUp();

            _readCallback = new EnvelopeCallback<ClientMessage.ReadEventCompleted>();

            _eventId = Guid.NewGuid();

            _eventData = Encoding.UTF8.GetBytes("test data");
            _eventMetadata = Encoding.UTF8.GetBytes("test metadata");
            _eventStreamId = "test-stream";
            _eventType = "someType";
            _expectedVersion = -2;

            _writeCallback = new EnvelopeCallback<ClientMessage.WriteEventsCompleted>();
            Publish(new ClientMessage.WriteEvents(Guid.NewGuid(),
                                                  new SendToThisEnvelope(_writeCallback), 
                                                  RoutingStrategy.AllowForwarding, 
                                                  _eventStreamId,
                                                  _expectedVersion, 
                                                  new Event(_eventId, _eventType, false, _eventData, _eventMetadata)));

            _writeCallback.Wait();

            Publish(new ClientMessage.ReadEvent(Guid.NewGuid(), new SendToThisEnvelope(_readCallback), _eventStreamId, 1, false));
        }

        protected override void TearDown()
        {
            if (_readCallback != null)
                _readCallback.Dispose();

            if (_writeCallback != null)
                _writeCallback.Dispose();

            _eventId = default(Guid);
            _eventData = null;
            _eventMetadata = null;
            _eventStreamId = null;
            _eventType = null;
            _expectedVersion = int.MinValue;
            _readCallback = null;
            _writeCallback = null;

            base.TearDown();
        }

        [Test]
        public void it_should_be_read_successfully()
        {
            _readCallback.Wait(msg => Assert.That(msg.Result == SingleReadResult.Success));
        }

        [Test]
        public void it_should_have_same_event_id()
        {
            _readCallback.Wait(msg => Assert.That(msg.Record.EventId == _eventId));
        }

        [Test]
        public void it_should_have_same_data()
        {
            _readCallback.Wait(msg => CollectionAssert.AreEqual(msg.Record.Data, _eventData));
        }

        [Test]
        public void it_should_have_same_metadata()
        {
            _readCallback.Wait(msg => CollectionAssert.AreEqual(msg.Record.Metadata, _eventMetadata));
        }

        [Test]
        public void it_should_have_same_event_type()
        {
            _readCallback.Wait(msg => Assert.That(msg.Record.EventType == _eventType));
        }

        [Test]
        public void it_should_have_same_expected_version()
        {
            _readCallback.Wait(msg => Assert.That(msg.Record.ExpectedVersion == _expectedVersion));
        }

        [Test]
        public void it_should_have_same_stream_id()
        {
            _readCallback.Wait(msg => Assert.That(msg.Record.EventStreamId == _eventStreamId));
        }

    }
}
