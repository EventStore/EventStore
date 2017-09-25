using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkDbConfig
    {
        public readonly string Path;
        public readonly int ChunkSize;
        public readonly long MaxChunksCacheSize;
        public readonly ICheckpoint WriterCheckpoint;
        public readonly ICheckpoint ChaserCheckpoint;
        public readonly ICheckpoint EpochCheckpoint;
        public readonly ICheckpoint TruncateCheckpoint;
        public readonly ICheckpoint ReplicationCheckpoint;
        public readonly IFileNamingStrategy FileNamingStrategy;
        public readonly bool InMemDb;
        public readonly bool Unbuffered;
        public readonly bool WriteThrough;

        public TFChunkDbConfig(string path, 
                               IFileNamingStrategy fileNamingStrategy, 
                               int chunkSize,
                               long maxChunksCacheSize,
                               ICheckpoint writerCheckpoint, 
                               ICheckpoint chaserCheckpoint,
                               ICheckpoint epochCheckpoint,
                               ICheckpoint truncateCheckpoint,
                               ICheckpoint replicationCheckpoint,
                               bool inMemDb = false,
                               bool unbuffered = false,
                               bool writethrough = false)
        {
            Ensure.NotNullOrEmpty(path, "path");
            Ensure.NotNull(fileNamingStrategy, "fileNamingStrategy");
            Ensure.Positive(chunkSize, "chunkSize");
            Ensure.Nonnegative(maxChunksCacheSize, "maxChunksCacheSize");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
            Ensure.NotNull(chaserCheckpoint, "chaserCheckpoint");
            Ensure.NotNull(epochCheckpoint, "epochCheckpoint");
            Ensure.NotNull(truncateCheckpoint, "truncateCheckpoint");
            Ensure.NotNull(replicationCheckpoint, "replicationCheckpoint");
            
            Path = path;
            ChunkSize = chunkSize;
            MaxChunksCacheSize = maxChunksCacheSize;
            WriterCheckpoint = writerCheckpoint;
            ChaserCheckpoint = chaserCheckpoint;
            EpochCheckpoint = epochCheckpoint;
            TruncateCheckpoint = truncateCheckpoint;
            ReplicationCheckpoint = replicationCheckpoint;
            FileNamingStrategy = fileNamingStrategy;
            InMemDb = inMemDb;
            Unbuffered = unbuffered;
            WriteThrough = writethrough;
        }

         public TFChunkDbConfig(string path, 
                               IFileNamingStrategy fileNamingStrategy, 
                               int chunkSize,
                               long maxChunksCacheSize,
                               ICheckpoint writerCheckpoint, 
                               ICheckpoint chaserCheckpoint,
                               ICheckpoint epochCheckpoint,
                               ICheckpoint truncateCheckpoint,
                               bool inMemDb = false,
                               bool unbuffered = false,
                               bool writethrough = false)
        : this(path, fileNamingStrategy, chunkSize, maxChunksCacheSize, writerCheckpoint, chaserCheckpoint,
               epochCheckpoint, truncateCheckpoint, new InMemoryCheckpoint(-1), inMemDb, unbuffered, writethrough)
        {
        }
    }
}