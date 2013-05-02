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
            var readerBuilder = new ReaderStrategy.Builder();
            builder.FromAll();
            builder.AllEvents();
            readerBuilder.FromAll();
            readerBuilder.AllEvents();
            var cs = builder.Build(ProjectionConfig.GetTest(), readerBuilder.Build(ProjectionConfig.GetTest()));
        }

    }
}
