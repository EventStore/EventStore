using System;

namespace EventStore.Core.Settings
{
    public static class ESConsts
    {
        public const int StorageReaderThreadCount = 4;

        public const int PTableInitialReaderCount = 5;
        public const int PTableMaxReaderCount = 1 /* StorageWriter */
                                              + 1 /* StorageChaser */
                                              + 1 /* Projections */
                                              + 1 /* Scavenging */
                                              + 1 /* Subscription LinkTos resolving */
                                              + StorageReaderThreadCount
                                              + 5 /* just in case reserve :) */;

        public const int TFChunkInitialReaderCount = 5;
        public const int TFChunkMaxReaderCount = PTableMaxReaderCount 
                                               + 2 /* for caching/uncaching, populating midpoints */
                                               + 1 /* for epoch manager usage of elections/replica service */
                                               + 1 /* for epoch manager usage of master replication service */;

        public const int MemTableEntryCount = 1000000;
        public const int StreamInfoCacheCapacity = 100000;
        public const int TransactionMetadataCacheCapacity = 50000;
        public const int CommitedEventsMemCacheLimit = 8*1024*1024;
        public const int CachedEpochCount = 1000;

        public const int CachedPrincipalCount = 1000;

        public static readonly TimeSpan InternalHeartbeatInterval = TimeSpan.FromMilliseconds(700);
        public static readonly TimeSpan InternalHeartbeatTimeout = TimeSpan.FromMilliseconds(1000);
        public static readonly TimeSpan ExternalHeartbeatInterval = TimeSpan.FromMilliseconds(3000);
        public static readonly TimeSpan ExternalHeartbeatTimeout = TimeSpan.FromMilliseconds(5000);

        public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);
    }
}
