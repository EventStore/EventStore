using EventStore.Core.Settings;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.Util;

namespace EventStore.Core.Tests.TransactionLog {
	public static class TFChunkHelper {
		public const int TFChunkInitialReaderCountDefault = Opts.ChunkInitialReaderCountDefault;
		public const int TFChunkMaxReaderCountDefault = ESConsts.PTableMaxReaderCount
		                                                + 2 /* for caching/uncaching, populating midpoints */
		                                                + 1 /* for epoch manager usage of elections/replica service */
		                                                + 1 /* for epoch manager usage of master replication service */;
		public static TFChunkDbConfig CreateDbConfig(string pathName, long writerCheckpointPosition,
			long chaserCheckpointPosition = 0,
			long epochCheckpointPosition = -1, long truncateCheckpoint = -1, int chunkSize = 10000) {
			return new TFChunkDbConfig(pathName,
				new VersionedPatternFileNamingStrategy(pathName, "chunk-"),
				chunkSize,
				0,
				new InMemoryCheckpoint(writerCheckpointPosition),
				new InMemoryCheckpoint(chaserCheckpointPosition),
				new InMemoryCheckpoint(epochCheckpointPosition),
				new InMemoryCheckpoint(truncateCheckpoint),
				new InMemoryCheckpoint(-1),
				TFChunkInitialReaderCountDefault,
				TFChunkMaxReaderCountDefault);
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
				TFChunkInitialReaderCountDefault,
				TFChunkMaxReaderCountDefault);
		}

		public static TFChunk CreateNewChunk(string fileName, int chunkSize = 4096, bool isScavenged = false) {
			return TFChunk.CreateNew(fileName, chunkSize, 0, 0,
				isScavenged: isScavenged, inMem: false, unbuffered: false,
				writethrough: false, initialReaderCount: TFChunkInitialReaderCountDefault, maxReaderCount: TFChunkMaxReaderCountDefault, reduceFileCachePressure: false);
		}
	}
}
