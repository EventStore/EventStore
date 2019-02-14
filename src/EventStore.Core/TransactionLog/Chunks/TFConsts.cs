using System;

namespace EventStore.Core.TransactionLog.Chunks {
	public static class TFConsts {
		public const int MaxLogRecordSize = 16 * 1024 * 1024; // 16Mb ought to be enough for everything?.. ;)

		public const int MidpointsDepth = 10;

		public const int ChunkSize = 256 * 1024 * 1024;
		public const int ChunksCacheSize = 2 * (ChunkSize + ChunkHeader.Size + ChunkFooter.Size);

		public static TimeSpan MinFlushDelayMs = TimeSpan.FromMilliseconds(2);
	}
}
