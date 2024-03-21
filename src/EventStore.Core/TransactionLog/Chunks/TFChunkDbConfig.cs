using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.TransactionLog.Chunks {
	public class TFChunkDbConfig {
		public readonly string Path;
		public readonly int ChunkSize;
		public readonly long MaxChunksCacheSize;
		public readonly ICheckpoint WriterCheckpoint;
		public readonly ICheckpoint ChaserCheckpoint;
		public readonly ICheckpoint EpochCheckpoint;
		public readonly ICheckpoint ProposalCheckpoint;
		public readonly ICheckpoint TruncateCheckpoint;
		public readonly ICheckpoint ReplicationCheckpoint;
		public readonly ICheckpoint IndexCheckpoint;
		public readonly ICheckpoint StreamExistenceFilterCheckpoint;
		public readonly IVersionedFileNamingStrategy FileNamingStrategy;
		public readonly bool InMemDb;
		public readonly bool Unbuffered;
		public readonly bool WriteThrough;
		public readonly int MaxReaderCount;
		public readonly bool OptimizeReadSideCache;
		public readonly bool ReduceFileCachePressure;
		public readonly long MaxTruncation;

		public TFChunkDbConfig(string path,
			IVersionedFileNamingStrategy fileNamingStrategy,
			int chunkSize,
			long maxChunksCacheSize,
			ICheckpoint writerCheckpoint,
			ICheckpoint chaserCheckpoint,
			ICheckpoint epochCheckpoint,
			ICheckpoint proposalCheckpoint,
			ICheckpoint truncateCheckpoint,
			ICheckpoint replicationCheckpoint,
			ICheckpoint indexCheckpoint,
			ICheckpoint streamExistenceFilterCheckpoint,
			int maxReaderCount,
			bool inMemDb = false,
			bool unbuffered = false,
			bool writethrough = false,
			bool optimizeReadSideCache = false,
			bool reduceFileCachePressure = false,
			long maxTruncation = 256 * 1024 * 1024) {
			Ensure.NotNullOrEmpty(path, "path");
			Ensure.NotNull(fileNamingStrategy, "fileNamingStrategy");
			Ensure.Positive(chunkSize, "chunkSize");
			Ensure.Nonnegative(maxChunksCacheSize, "maxChunksCacheSize");
			Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
			Ensure.NotNull(chaserCheckpoint, "chaserCheckpoint");
			Ensure.NotNull(epochCheckpoint, "epochCheckpoint");
			Ensure.NotNull(proposalCheckpoint, "proposalCheckpoint");
			Ensure.NotNull(truncateCheckpoint, "truncateCheckpoint");
			Ensure.NotNull(replicationCheckpoint, "replicationCheckpoint");
			Ensure.NotNull(indexCheckpoint, "indexCheckpoint");
			Ensure.NotNull(streamExistenceFilterCheckpoint, "streamExistenceFilterCheckpoint");
			Ensure.Positive(maxReaderCount, "maxReaderCount");

			Path = path;
			ChunkSize = chunkSize;
			MaxChunksCacheSize = maxChunksCacheSize;
			WriterCheckpoint = writerCheckpoint;
			ChaserCheckpoint = chaserCheckpoint;
			EpochCheckpoint = epochCheckpoint;
			ProposalCheckpoint = proposalCheckpoint;
			TruncateCheckpoint = truncateCheckpoint;
			ReplicationCheckpoint = replicationCheckpoint;
			IndexCheckpoint = indexCheckpoint;
			StreamExistenceFilterCheckpoint = streamExistenceFilterCheckpoint;
			FileNamingStrategy = fileNamingStrategy;
			InMemDb = inMemDb;
			Unbuffered = unbuffered;
			WriteThrough = writethrough;
			MaxReaderCount = maxReaderCount;
			OptimizeReadSideCache = optimizeReadSideCache;
			ReduceFileCachePressure = reduceFileCachePressure;
			MaxTruncation = maxTruncation;
		}
	}
}
