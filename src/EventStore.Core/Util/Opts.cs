using System;
using System.Net;
using EventStore.Common.Options;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Util {
	public static class Opts {
		public const int ConnectionPendingSendBytesThresholdDefault = 10 * 1024 * 1024;
		
		public const int ConnectionQueueSizeThresholdDefault = 50000;
		
		public const int HashCollisionReadLimitDefault = 100;

		public const int ChunkInitialReaderCountDefault = 5;

		public const int ReaderThreadsCountDefault = 4;

		public const bool FaultOutOfOrderProjectionsDefault = false;

		public const int ProjectionsQueryExpiryDefault = 5;

		public const string CertificateReservedNodeCommonNameDefault = "eventstoredb-node";

		public const byte IndexBitnessVersionDefault = Index.PTableVersions.IndexV4;

		public static readonly string AuthenticationTypeDefault = "internal";

		public const bool SkipIndexScanOnReadsDefault = false;
	}
}
