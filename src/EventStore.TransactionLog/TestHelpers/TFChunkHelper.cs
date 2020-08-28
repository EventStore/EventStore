using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.TransactionLog.TestHelpers {
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

		public static TFChunk CreateNewChunk(string fileName, int chunkSize = 4096, bool isScavenged = false) {
			return TFChunk.CreateNew(fileName, chunkSize, 0, 0,
				isScavenged: isScavenged, inMem: false, initialReaderCount: 5/*Constants.TFChunkInitialReaderCountDefault*/, maxReaderCount: 21/*Constants.TFChunkMaxReaderCountDefault*/);
		}
	}
}
