using System;
using EventStore.Core.Messages;

namespace EventStore.Core.TransactionLog.Chunks
{
    public interface ITFChunkScavengerLog
    {
        string ScavengeId { get; }
        
        void ScavengeStarted();

        void ChunksScavenged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, long spaceSaved);

        void ChunksNotScavenged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, long spaceSaved, string errorMessage);

        void ScavengeCompleted(ScavengeResult result, string error, long spaceSaved, TimeSpan elapsed);
    }
    
    public enum ScavengeResult
    {
        Success,
        Failed,
        Stopped
    }
}
