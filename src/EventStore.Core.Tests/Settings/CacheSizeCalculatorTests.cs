﻿using EventStore.Core.Settings;
using NUnit.Framework;

namespace EventStore.Core.Tests.Settings {
	[TestFixture]
	public class CacheSizeCalculatorTests {
		const ulong GigaByte = CacheSizeCalculator.Gigabyte;
		const ulong ESMem = 800 * 1000 * 1000;

		[TestCase]
		public void configured_takes_precedence() => Test(configuredCapacity: 1, mem: GigaByte, expected: 1);

		[TestCase]
		public void enforces_minimum() => Test(configuredCapacity: 0, mem: 200, expected: 100_000);

		[TestCase]
		public void at_001gib() => Test(configuredCapacity: 0, mem: 1 * GigaByte - ESMem, expected: 100_000);

		[TestCase]
		public void at_004gib() => Test(configuredCapacity: 0, mem: 4 * GigaByte - ESMem, expected: 2_000_000);

		[TestCase]
		public void at_008gib() => Test(configuredCapacity: 0, mem: 8 * GigaByte - ESMem, expected: 6_000_000);

		[TestCase]
		public void at_016gib() => Test(configuredCapacity: 0, mem: 16 * GigaByte - ESMem, expected: 12_000_000);

		[TestCase]
		public void at_032gib() => Test(configuredCapacity: 0, mem: 32 * GigaByte - ESMem, expected: 24_000_000);

		[TestCase]
		public void at_064gib() => Test(configuredCapacity: 0, mem: 64 * GigaByte - ESMem, expected: 48_000_000);

		[TestCase]
		public void at_128gib() => Test(configuredCapacity: 0, mem: 128 * GigaByte - ESMem, expected: 96_000_000);

		public static void Test(int configuredCapacity, ulong mem, int expected) =>
			Assert.AreEqual(expected, CacheSizeCalculator.CalculateStreamInfoCacheCapacity(configuredCapacity, mem));
	}
}
