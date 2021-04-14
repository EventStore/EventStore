using System;

namespace EventStore.Core.Settings {

	public static class ThreadCountCalculator {
		private const int ReaderThreadCountFloor = 4;

		public static int CalculateReaderThreadCount(int configuredCount, ulong availableMemoryBytes) {
			if (configuredCount > 0)
				return configuredCount;

			var estimatedGigabytes = Math.Floor(availableMemoryBytes / (float)CacheSizeCalculator.Gigabyte);
			var readerCount = Math.Clamp(estimatedGigabytes * 2, ReaderThreadCountFloor, 16);

			return (int)readerCount;
		}

		public static int CalculateWorkerThreadCount(int configuredCount, int readerCount) {
			if (configuredCount > 0)
				return configuredCount;

			return readerCount > ReaderThreadCountFloor ? 10 : 5;
		}
	}
}
