using EventStore.Core.Services.Storage.StorageChunk;
using EventStore.Core.TransactionLogV2.Checkpoint;
using EventStore.Core.TransactionLogV2.FileNamingStrategy;

namespace EventStore.Core.Services.Storage.TestHelpers {
	// TODO: Consider moving this into test projects
	public static class TFChunkHelper {
		public static TFChunkDbConfig CreateDbConfig(string pathName, long writerCheckpointPosition,
			long chaserCheckpointPosition = 0,
			long epochCheckpointPosition = -1, long truncateCheckpoint = -1, int chunkSize = 10000,
			long maxTruncation = -1) {
			return new TFChunkDbConfig(pathName,
				new VersionedPatternFileNamingStrategy(pathName, "chunk-"),
				chunkSize,
				0,
				new InMemoryCheckpoint(writerCheckpointPosition),
				new InMemoryCheckpoint(chaserCheckpointPosition),
				new InMemoryCheckpoint(epochCheckpointPosition),
				new InMemoryCheckpoint(truncateCheckpoint),
				new InMemoryCheckpoint(-1), 
				new InMemoryCheckpoint(-1),
				5, //Constants.TFChunkInitialReaderCountDefault, 
				21,//Constants.TFChunkMaxReaderCountDefault,
				maxTruncation: maxTruncation);
		}

		public static TFChunkDbConfig CreateDbConfig(string pathName, ICheckpoint writerCheckpoint,
			ICheckpoint chaserCheckpoint, int chunkSize = 10000, ICheckpoint replicationCheckpoint = null) {
			if (replicationCheckpoint == null) replicationCheckpoint = new InMemoryCheckpoint(-1);
			return new TFChunkDbConfig(pathName,
				new VersionedPatternFileNamingStrategy(pathName, "chunk-"),
				chunkSize,
				0,
				writerCheckpoint,
				chaserCheckpoint,
				new InMemoryCheckpoint(-1),
				new InMemoryCheckpoint(-1),
				replicationCheckpoint,
				new InMemoryCheckpoint(-1),
				5, //Constants.TFChunkInitialReaderCountDefault, 
				21);//Constants.TFChunkMaxReaderCountDefault);
		}
	}
}
