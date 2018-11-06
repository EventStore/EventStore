using System.Threading;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging
{
    [TestFixture]
    class when_scavenge_cancelled_after_started : ScavengeLifeCycleScenario
    {
        protected override void When()
        {
            var cancellationTokenSource = new CancellationTokenSource();

            Log.StartedCallback += (sender, args) => cancellationTokenSource.Cancel();
            TfChunkScavenger.Scavenge(false, true, 0, cancellationTokenSource.Token).Wait();
        }

        [Test]
        public void completed_logged_with_stopped_result()
        {

            Assert.That(Log.Completed);
            Assert.That(Log.Result, Is.EqualTo(ScavengeResult.Stopped));
        }

        [Test]
        public void no_chunks_scavenged()
        {
            Assert.That(Log.Scavenged, Is.Empty);
        }

    }
}