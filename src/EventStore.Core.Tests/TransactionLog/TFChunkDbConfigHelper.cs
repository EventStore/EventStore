using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.Tests.TransactionLog
{
    public static class TFChunkDbConfigHelper
    {
        public static TFChunkDbConfig Create(string pathName, long writerCheckpointPosition, long chaserCheckpointPosition = 0,
            long epochCheckpointPosition = -1, long truncateCheckpoint = -1, int chunkSize = 10000)
        {
            return new TFChunkDbConfig(pathName,
                                             new VersionedPatternFileNamingStrategy(pathName, "chunk-"),
                                             chunkSize,
                                             0,
                                             new InMemoryCheckpoint(writerCheckpointPosition),
                                             new InMemoryCheckpoint(chaserCheckpointPosition),
                                             new InMemoryCheckpoint(epochCheckpointPosition),
                                             new InMemoryCheckpoint(truncateCheckpoint),
                                             new InMemoryCheckpoint(-1));
        }

        public static TFChunkDbConfig Create(string pathName, ICheckpoint writerCheckpoint, ICheckpoint chaserCheckpoint, int chunkSize = 10000, ICheckpoint replicationCheckpoint = null)
        {
            if(replicationCheckpoint == null) replicationCheckpoint = new InMemoryCheckpoint(-1);
            return new TFChunkDbConfig(pathName,
                                             new VersionedPatternFileNamingStrategy(pathName, "chunk-"),
                                             chunkSize,
                                             0,
                                             writerCheckpoint,
                                             chaserCheckpoint,
                                             new InMemoryCheckpoint(-1),
                                             new InMemoryCheckpoint(-1),
                                             replicationCheckpoint);
        }
    }
}