﻿using System.Threading;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging
{
    [TestFixture]
    class when_scavenge_cancelled_after_chunck_scavenged : ScavengeLifeCycleScenario
    {
        protected override void When()
        {
            var cancellationTokenSource = new CancellationTokenSource();

            Log.ChunkScavenged += (sender, args) => cancellationTokenSource.Cancel();
            TfChunkScavenger.Scavenge(true, true, cancellationTokenSource.Token).Wait();
        }

        [Test]
        public void completed_logged_with_stopped_result()
        {
            Assert.That(Log.Completed);
            Assert.That(Log.Result, Is.EqualTo(ScavengeResult.Stopped));
        }

        [Test]
        public void scavenge_record_for_first_and_cancelled_chunk()
        {
            Assert.That(Log.Scavenged, Has.Count.EqualTo(1));
            Assert.That(Log.Scavenged[0].Scavenged, Is.True);
        }

    }
}