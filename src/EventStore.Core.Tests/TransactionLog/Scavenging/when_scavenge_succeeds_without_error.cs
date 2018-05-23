using System.Threading;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging
{
    [TestFixture]
    class when_scavenge_succeeds_without_error : ScavengeLifeCycleScenario
    {
        protected override void When()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            TfChunkScavenger.Scavenge(true, true, 0, cancellationTokenSource.Token).Wait();
        }

        [Test]
        public void log_started()
        {
            Assert.That(Log.Started);
        }

        [Test]
        public void log_completed_with_success()
        {
            Assert.That(Log.Completed);
            Assert.That(Log.Result, Is.EqualTo(ScavengeResult.Success));
        }

        [Test]
        public void scavenge_record_for_all_completed_chunks_plus_merge()
        {
            Assert.That(Log.Scavenged, Has.Count.EqualTo(3));
            Assert.That(Log.Scavenged[0].Scavenged, Is.True);
            Assert.That(Log.Scavenged[0].ChunkStart, Is.EqualTo(0));
            Assert.That(Log.Scavenged[0].ChunkEnd, Is.EqualTo(0));            
            Assert.That(Log.Scavenged[1].Scavenged, Is.True);
            Assert.That(Log.Scavenged[1].ChunkStart, Is.EqualTo(1));
            Assert.That(Log.Scavenged[1].ChunkEnd, Is.EqualTo(1));
            Assert.That(Log.Scavenged[2].Scavenged, Is.True);
            Assert.That(Log.Scavenged[2].ChunkStart, Is.EqualTo(0));
            Assert.That(Log.Scavenged[2].ChunkEnd, Is.EqualTo(1));
        }

        [Test]
        public void calls_scavenge_on_the_table_index()
        {
            Assert.That(FakeTableIndex.ScavengeCount, Is.EqualTo(1));
        }
    }
}
