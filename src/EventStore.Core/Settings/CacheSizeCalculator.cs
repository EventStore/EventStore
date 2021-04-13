namespace EventStore.Core.Settings {
	public static class CacheSizeCalculator {
		// each time we increase the capacity by 1 we need about this much more memory
		const ulong BytesPerUnitCapacity = 1024;
		public const long Gibibyte = 1L * 1024 * 1024 * 1024;

		public static int CalculateStreamInfoCacheCapacity(int configuredCapacity, ulong availableMemoryBytes) {
			if (configuredCapacity > 0)
				return configuredCapacity;

			// determine cache sizes based on available memory
			// always have at least 100k
			const int baseCapacity = 100_000;

			// grow the capacity linearly with memory above 2gib
			if (availableMemoryBytes <= 2 * Gibibyte)
				return baseCapacity;

			availableMemoryBytes -= 2 * Gibibyte;

			// allow 1kib for each stream. nb the memory will only be used on demand
			var extraCapacity = (int)(availableMemoryBytes / BytesPerUnitCapacity);

			return baseCapacity + extraCapacity;
		}
	}
}
