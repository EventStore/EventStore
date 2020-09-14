using EventStore.Common.Utils;
using EventStore.Core.TransactionLogV2.Checkpoint;
using EventStore.Core.TransactionLogV2.FileNamingStrategy;

namespace EventStore.Core.Services.Storage.StorageChunk
{
	public class TFChunkDbConfig {
		public readonly string Path;
		public readonly int ChunkSize;
		public readonly long MaxChunksCacheSize;
		public readonly ICheckpoint WriterCheckpoint;
		public readonly ICheckpoint ChaserCheckpoint;
		public readonly ICheckpoint EpochCheckpoint;
		public readonly ICheckpoint TruncateCheckpoint;
		public readonly ICheckpoint ReplicationCheckpoint;
		public readonly ICheckpoint IndexCheckpoint;
		public readonly IFileNamingStrategy FileNamingStrategy;
		public readonly bool InMemDb;
		public readonly int InitialReaderCount;
		public readonly int MaxReaderCount;
		public readonly bool OptimizeReadSideCache;
		public readonly long MaxTruncation;

		public TFChunkDbConfig(string path,
			IFileNamingStrategy fileNamingStrategy,
			int chunkSize,
			long maxChunksCacheSize,
			ICheckpoint writerCheckpoint,
			ICheckpoint chaserCheckpoint,
			ICheckpoint epochCheckpoint,
			ICheckpoint truncateCheckpoint,
			ICheckpoint replicationCheckpoint,
			ICheckpoint indexCheckpoint,
			int initialReaderCount,
			int maxReaderCount,
			bool inMemDb = false,
			bool optimizeReadSideCache = false,
			long maxTruncation = 256 * 1024 * 1024) {
			Ensure.NotNullOrEmpty(path, "path");
			Ensure.NotNull(fileNamingStrategy, "fileNamingStrategy");
			Ensure.Positive(chunkSize, "chunkSize");
			Ensure.Nonnegative(maxChunksCacheSize, "maxChunksCacheSize");
			Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
			Ensure.NotNull(chaserCheckpoint, "chaserCheckpoint");
			Ensure.NotNull(epochCheckpoint, "epochCheckpoint");
			Ensure.NotNull(truncateCheckpoint, "truncateCheckpoint");
			Ensure.NotNull(replicationCheckpoint, "replicationCheckpoint");
			Ensure.NotNull(indexCheckpoint, "indexCheckpoint");
			Ensure.Positive(initialReaderCount, "initialReaderCount");
			Ensure.Positive(maxReaderCount, "maxReaderCount");

			Path = path;
			ChunkSize = chunkSize;
			MaxChunksCacheSize = maxChunksCacheSize;
			WriterCheckpoint = writerCheckpoint;
			ChaserCheckpoint = chaserCheckpoint;
			EpochCheckpoint = epochCheckpoint;
			TruncateCheckpoint = truncateCheckpoint;
			ReplicationCheckpoint = replicationCheckpoint;
			IndexCheckpoint = indexCheckpoint;
			FileNamingStrategy = fileNamingStrategy;
			InMemDb = inMemDb;
			InitialReaderCount = initialReaderCount;
			MaxReaderCount = maxReaderCount;
			OptimizeReadSideCache = optimizeReadSideCache;
			MaxTruncation = maxTruncation;
		}

		public TransactionLogV2.Chunks.TFChunkDbConfig GetV2Config() {
			return new TransactionLogV2.Chunks.TFChunkDbConfig(Path, FileNamingStrategy, ChunkSize, MaxChunksCacheSize, WriterCheckpoint,
				ChaserCheckpoint, EpochCheckpoint, TruncateCheckpoint, ReplicationCheckpoint, IndexCheckpoint,
				InitialReaderCount, MaxReaderCount, InMemDb, OptimizeReadSideCache, MaxTruncation);
		}
	}
}
