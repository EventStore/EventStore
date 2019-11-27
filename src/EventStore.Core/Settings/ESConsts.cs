﻿using System;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Settings {
	public static class ESConsts {
		public const int StorageReaderThreadCount = 4;

		public const int PTableInitialReaderCount = 5;

		public const int PTableMaxReaderCount = 1 /* StorageWriter */
		                                        + 1 /* StorageChaser */
		                                        + 1 /* Projections */
		                                        + TFChunkScavenger.MaxThreadCount /* Scavenging (1 per thread) */
		                                        + 1 /* Subscription LinkTos resolving */
		                                        + StorageReaderThreadCount
		                                        + 5 /* just in case reserve :) */;

		public const int MemTableEntryCount = 1000000;
		public const int StreamInfoCacheCapacity = 100000;
		public const int TransactionMetadataCacheCapacity = 50000;
		public const int CommitedEventsMemCacheLimit = 8 * 1024 * 1024;
		public const int CachedEpochCount = 1000;
		public const int ReadRequestTimeout = 10000;

		public const int CachedPrincipalCount = 1000;

		public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

		public const int UnrestrictedPendingSendBytes = 0;
		public const int MaxConnectionQueueSize = 50000;
	}
}
