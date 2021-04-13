using EventStore.Core.Settings;
using NUnit.Framework;

namespace EventStore.Core.Tests.Settings {
	[TestFixture]
	public class CacheSizeCalculatorTests {
		const ulong Gibibyte = CacheSizeCalculator.Gibibyte;

		[TestCase]
		public void configured_takes_precedence() => Test(configuredCapacity: 1, mem: Gibibyte, expected: 1);

		[TestCase]
		public void enforces_minimum() => Test(configuredCapacity: 0, mem: Gibibyte, expected: 100_000);

		[TestCase]
		public void just_before_threshold() => Test(configuredCapacity: 0, mem: 2 * Gibibyte, expected: 100_000);

		[TestCase]
		public void onek_above_threshold() => Test(configuredCapacity: 0, mem: 2 * Gibibyte + 1024, expected: 100_001);

		[TestCase]
		public void at_01gib() => Test(configuredCapacity: 0, mem: 1 * Gibibyte, expected: 100_000);

		[TestCase]
		public void at_02gib() => Test(configuredCapacity: 0, mem: 2 * Gibibyte, expected: 100_000);

		[TestCase]
		public void at_04gib() => Test(configuredCapacity: 0, mem: 4 * Gibibyte, expected: 2_197_152);

		[TestCase]
		public void at_08gib() => Test(configuredCapacity: 0, mem: 8 * Gibibyte, expected: 6_391_456);

		[TestCase]
		public void at_16gib() => Test(configuredCapacity: 0, mem: 16 * Gibibyte, expected: 14_780_064);

		[TestCase]
		public void at_32gib() => Test(configuredCapacity: 0, mem: 32 * Gibibyte, expected: 31_557_280);

		public static void Test(int configuredCapacity, ulong mem, int expected) =>
			Assert.AreEqual(expected, CacheSizeCalculator.CalculateStreamInfoCacheCapacity(configuredCapacity, mem));
	}
}
