using System;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.checkpoint_strategy
{
    [TestFixture]
    public class the_from_category_checkpoint_strategy
    {
        private CheckpointStrategy _strategy;

        [SetUp]
        public void setup()
        {
            var builder = new CheckpointStrategy.Builder();
            builder.FromCategory("category1");
            builder.AllEvents();
            _strategy = builder.Build(ProjectionConfig.GetTest());
        }

    }
}