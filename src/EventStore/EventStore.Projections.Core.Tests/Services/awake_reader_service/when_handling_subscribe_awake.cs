using System;
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
    public class when_handling_subscribe_awake
    {
        private AwakeReaderService _it;
        private EventRecord _eventRecord;
        private StorageMessage.EventCommitted _eventCommitted;
        private Exception _exception;
        private IEnvelope _envelope;

        [SetUp]
        public void SetUp()
        {
            _exception = null;
            Given();
            When();
        }

        private void Given()
        {
            _it = new AwakeReaderService();

            _eventRecord = new EventRecord(
                10,
                new PrepareLogRecord(
                    500, Guid.NewGuid(), Guid.NewGuid(), 500, 0, "Stream", 99, DateTime.UtcNow, PrepareFlags.Data,
                    "event", new byte[0], null));
            _eventCommitted = new StorageMessage.EventCommitted(1000, _eventRecord, isTfEof: true);
            _envelope = new NoopEnvelope();
        }

        private void When()
        {
            try
            {
                _it.Handle(
                    new AwakeReaderServiceMessage.SubscribeAwake(
                        _envelope, Guid.NewGuid(), "Stream", new TFPos(1000, 500), new TestMessage()));
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

    }
}