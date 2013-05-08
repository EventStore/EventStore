using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.checkpoint_strategy
{
    [TestFixture]
    public class when_creating_a_checkpoint_strategy
    {
        [Test]
        public void it_can_be_created()
        {
            var builder = CheckpointStrategy.Create(
                new QuerySourcesDefinition {AllStreams = true, AllEvents = true}, ProjectionConfig.GetTest(),
                new RealTimeProvider());
        }

    }
}
