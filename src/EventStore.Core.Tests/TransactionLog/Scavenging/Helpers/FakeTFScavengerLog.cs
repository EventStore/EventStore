using System;
using System.Collections.Generic;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Tests.TransactionLog.Scavenging.Helpers
{
    public class FakeTFScavengerLog : ITFChunkScavengerLog
    {
        public string ScavengeId { get; } = "FakeScavenge";

        public bool Started { get; private set; }

        public bool Completed { get; private set; }

        public ScavengeResult Result { get; private set; }

        public event EventHandler<EventArgs> StartedCallback;
        public event EventHandler<ScavengedLog> ChunkScavenged;
        public event EventHandler<EventArgs> CompletedCallback;

        public IList<ScavengedLog> Scavenged { get; } = new List<ScavengedLog>(); 

        public void ScavengeStarted()
        {
            Started = true;
            StartedCallback?.Invoke(this, EventArgs.Empty);
        }

        public void ChunksScavenged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, long spaceSaved)
        {
            var scavengedLog = new ScavengedLog(chunkStartNumber, chunkEndNumber, true, "");
            Scavenged.Add(scavengedLog);
            ChunkScavenged?.Invoke(this, scavengedLog);
        }

        public void ChunksNotScavenged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed,
            string errorMessage)
        {
            var scavengedLog = new ScavengedLog(chunkStartNumber, chunkEndNumber, false, "");
            Scavenged.Add(scavengedLog);
            ChunkScavenged?.Invoke(this, scavengedLog);

        }

        public void ChunksMerged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, long spaceSaved)
        {
            var scavengedLog = new ScavengedLog(chunkStartNumber, chunkEndNumber, true, "");
            Scavenged.Add(scavengedLog);
            ChunkScavenged?.Invoke(this, scavengedLog);
        }

        public void ChunksNotMerged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, string errorMessage)
        {
            var scavengedLog = new ScavengedLog(chunkStartNumber, chunkEndNumber, false, "");
            Scavenged.Add(scavengedLog);
            ChunkScavenged?.Invoke(this, scavengedLog);
        }

        public void ScavengeCompleted(ScavengeResult result, string error, TimeSpan elapsed)
        {
            Completed = true;
            Result = result;
            CompletedCallback?.Invoke(this, EventArgs.Empty);
        }

        public class ScavengedLog
        {
            public int ChunkStart { get; }
            public int ChunkEnd { get; }
            public bool Scavenged { get; }
            public string Error { get; }

            public ScavengedLog(int chunkStart, int chunkEnd, bool scavenged, string error)
            {
                ChunkStart = chunkStart;
                ChunkEnd = chunkEnd;
                Scavenged = scavenged;
                Error = error;
            }
        }
    }

    
}