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
		public readonly ICheckpoint TruncateCheckpoint;
		public readonly ICheckpoint ReplicationCheckpoint;
		public readonly IFileNamingStrategy FileNamingStrategy;
		public readonly bool InMemDb;
		public readonly bool Unbuffered;
		public readonly bool WriteThrough;
		public readonly int InitialReaderCount;
		public readonly bool OptimizeReadSideCache;
		public readonly bool ReduceFileCachePressure;

		public TFChunkDbConfig(string path,
			IFileNamingStrategy fileNamingStrategy,
			int chunkSize,
			long maxChunksCacheSize,
			ICheckpoint writerCheckpoint,
			ICheckpoint chaserCheckpoint,
			ICheckpoint epochCheckpoint,
			ICheckpoint truncateCheckpoint,
			ICheckpoint replicationCheckpoint,
			int initialReaderCount,
			bool inMemDb = false,
			bool unbuffered = false,
			bool writethrough = false,
			bool optimizeReadSideCache = false,
			bool reduceFileCachePressure = false) {
			Ensure.NotNullOrEmpty(path, "path");
			Ensure.NotNull(fileNamingStrategy, "fileNamingStrategy");
			Ensure.Positive(chunkSize, "chunkSize");
			Ensure.Nonnegative(maxChunksCacheSize, "maxChunksCacheSize");
			Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
			Ensure.NotNull(chaserCheckpoint, "chaserCheckpoint");
			Ensure.NotNull(epochCheckpoint, "epochCheckpoint");
			Ensure.NotNull(truncateCheckpoint, "truncateCheckpoint");
			Ensure.NotNull(replicationCheckpoint, "replicationCheckpoint");
			Ensure.Positive(initialReaderCount, "initialReaderCount");

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
			InitialReaderCount = initialReaderCount;
			OptimizeReadSideCache = optimizeReadSideCache;
			ReduceFileCachePressure = reduceFileCachePressure;
		}
	}
}
