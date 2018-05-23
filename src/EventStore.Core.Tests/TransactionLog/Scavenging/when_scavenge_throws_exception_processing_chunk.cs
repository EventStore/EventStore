using System;
using System.Threading;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging
{
    [TestFixture]
    class when_scavenge_throws_exception_processing_chunk : ScavengeLifeCycleScenario
    {
        protected override void When()
        {
            var cancellationTokenSource = new CancellationTokenSource();

            Log.ChunkScavenged += (sender, args) =>
            {
                if (args.Scavenged)
                    throw new Exception("Expected exception.");
            };

            TfChunkScavenger.Scavenge(true, true, 0, cancellationTokenSource.Token).Wait();
        }

        [Test]
        public void no_exception_is_thrown_to_caller()
        {
            Assert.That(Log.Completed);
            Assert.That(Log.Result, Is.EqualTo(ScavengeResult.Failed));
        }
    }
}