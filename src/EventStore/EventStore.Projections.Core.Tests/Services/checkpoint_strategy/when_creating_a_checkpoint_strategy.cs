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
            var builder = new CheckpointStrategy.Builder();
            builder.FromAll();
            builder.AllEvents();
            var cs = builder.Build(ProjectionMode.Persistent);
        }

    }
}
