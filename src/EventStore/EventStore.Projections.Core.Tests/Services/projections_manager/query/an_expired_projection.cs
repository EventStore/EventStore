using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.query
{
    public class an_expired_projection
    {
        public abstract class Base : a_new_posted_projection.Base
        {
            protected Guid _reader;

            protected override void Given()
            {
                base.Given();
            }

            protected override IEnumerable<WhenStep> When()
            {
                foreach (var m in base.When()) yield return m;
                var readerAssignedMessage =
                    _consumer.HandledMessages.OfType<EventReaderSubscriptionMessage.ReaderAssignedReader>().LastOrDefault();
                Assert.IsNotNull(readerAssignedMessage);
                _reader = readerAssignedMessage.ReaderId;

                yield return
                    (ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
                        _reader, new TFPos(100, 50), new TFPos(100, 50), "stream", 1, "stream", 1, false, Guid.NewGuid(),
                        "type", false, new byte[0], new byte[0], 100, 33.3f));
                _timeProvider.AddTime(TimeSpan.FromMinutes(6));
                yield return Yield;
                foreach (var m in _consumer.HandledMessages.OfType<TimerMessage.Schedule>().ToArray())
                    m.Envelope.ReplyWith(m.ReplyMessage);
            }
        }

        [TestFixture]
        public class when_retrieving_statistics : Base
        {
            protected override IEnumerable<WhenStep> When()
            {
                foreach (var s in base.When()) yield return s;
                yield return (
                    new ProjectionManagementMessage.Command.GetStatistics(
                        new PublishEnvelope(_bus), null, _projectionName, false));
            }

            [Test]
            public void projection_is_not_found()
            {
                Assert.IsTrue(_consumer.HandledMessages.OfType<ProjectionManagementMessage.NotFound>().Any());
            }
        }
    }
}
