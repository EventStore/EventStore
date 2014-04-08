using System;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Fakes;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reordering_projection_subscription
{
    [TestFixture]
    public class when_creating_projection_subscription
    {
        [Test]
        public void it_can_be_created()
        {
            new EventReorderingReaderSubscription(new FakePublisher(),
                Guid.NewGuid(), CheckpointTag.FromPosition(0, 0, -1),
                CreateReaderStrategy(),
                1000, 2000, 500);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_publisher_throws_argument_null_exception()
        {
            new EventReorderingReaderSubscription(null,
                Guid.NewGuid(), CheckpointTag.FromPosition(0, 0, -1), 
                CreateReaderStrategy(),
                1000, 2000, 500);
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void null_describe_source_throws_argument_null_exception()
        {
            new EventReorderingReaderSubscription(new FakePublisher(),
                Guid.NewGuid(), CheckpointTag.FromPosition(0, 0, -1),
                null, 1000, 2000, 500);
        }

        private IReaderStrategy CreateReaderStrategy()
        {
            var result = new SourceDefinitionBuilder();
            result.FromAll();
            result.AllEvents();
            return ReaderStrategy.Create(
                "test",
                0,
                result.Build(),
                new RealTimeProvider(),
                stopOnEof: false,
                runAs: null);
        }
    }
}
