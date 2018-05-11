using System;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Tests.TransactionLog.Scavenging.Helpers
{
    public class FakeTFScavengerLog : ITFChunkScavengerLog
    {
        public string ScavengeId { get; } = "FakeScavenge";

        public void ScavengeStarted()
        {
            
        }

        public void ChunksScavenged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, long spaceSaved)
        {
        }

        public void ChunksNotScavenged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, long spaceSaved,
            string errorMessage)
        {
        }

        public void ScavengeCompleted(ScavengeResult result, string error, long spaceSaved, TimeSpan elapsed)
        {
        }
    }
}