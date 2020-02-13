using EventStore.Core.Settings;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;

namespace EventStore.Client {
	public class TestConstants {
		public const int PTableInitialReaderCount = ESConsts.PTableInitialReaderCount;

		public const int PTableMaxReaderCountDefault = 	1 /* StorageWriter */
		                                                + 1 /* StorageChaser */
		                                                + 1 /* Projections */
		                                                + 4 /* Scavenging (1 per thread) */
		                                                + 1 /* Subscription LinkTos resolving */
		                                                + Opts.ReaderThreadsCountDefault
		                                                + 5 /* just in case reserve :) */;

		public const int TFChunkInitialReaderCountDefault = Opts.ChunkInitialReaderCountDefault;

		public const int TFChunkMaxReaderCountDefault = PTableMaxReaderCountDefault
		                                                + 2 /* for caching/uncaching, populating midpoints */
		                                                + 1 /* for epoch manager usage of elections/replica service */
		                                                + 1 /* for epoch manager usage of leader replication service */;
	}
}
