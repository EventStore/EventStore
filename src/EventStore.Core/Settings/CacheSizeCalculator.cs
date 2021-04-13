namespace EventStore.Core.Settings {
	public static class CacheSizeCalculator {
		// each time we increase the capacity by 1 we need about this much more memory
		const ulong BytesPerUnitCapacity = 1000;
		public const long Gibibyte = 1L * 1000 * 1000 * 1000;

		public static int CalculateStreamInfoCacheCapacity(int configuredCapacity, ulong availableMemoryBytes) {
			if (configuredCapacity > 0)
				return configuredCapacity;

			var estimatedSystemMem = availableMemoryBytes + 800 * 1000 * 1000;

			if (estimatedSystemMem <= 1 * Gibibyte)
				return 100_000;

			double proportionOfMem;
			if (estimatedSystemMem <= 4 * Gibibyte)
				proportionOfMem = 0.5;
			else
				proportionOfMem = 0.75;

			var capacity = (int)(estimatedSystemMem * proportionOfMem/ BytesPerUnitCapacity);
			return capacity;

		}
	}
}
