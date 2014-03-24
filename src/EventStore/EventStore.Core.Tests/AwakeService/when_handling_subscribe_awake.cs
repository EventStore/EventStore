using System;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.AwakeReaderService;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.AwakeService
{
    [TestFixture]
    public class when_handling_subscribe_awake
    {
        private Core.Services.AwakeReaderService.AwakeService _it;
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
            _it = new Core.Services.AwakeReaderService.AwakeService();

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
                    new AwakeServiceMessage.SubscribeAwake(
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