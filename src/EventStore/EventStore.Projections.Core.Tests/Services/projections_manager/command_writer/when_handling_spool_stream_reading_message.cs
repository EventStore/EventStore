using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Commands;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.command_writer
{
    [TestFixture]
    class when_handling_spool_stream_reading_message : specification_with_projection_manager_command_writer
    {
        private Guid _workerId;
        private Guid _subscriptionId;

        protected override void Given()
        {
            _workerId = Guid.NewGuid();
            _subscriptionId = Guid.NewGuid();
        }

        protected override void When()
        {
            _sut.Handle(
                new ReaderSubscriptionManagement.SpoolStreamReading(_workerId, _subscriptionId, "stream1", 100, 1000000));
        }

        [Test]
        public void publishes_spool_stream_reading_command()
        {
            var command =
                AssertParsedSingleCommand<SpoolStreamReadingCommand>(
                    "$spool-stream-reading",
                    _workerId);
            
            Assert.AreEqual(_subscriptionId.ToString("N"), command.SubscriptionId);
            Assert.AreEqual("stream1", command.StreamId);
            Assert.AreEqual(100, command.CatalogSequenceNumber);
            Assert.AreEqual(1000000, command.LimitingCommitPosition);
        }

    }
}