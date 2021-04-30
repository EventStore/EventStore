using EventStore.Core.Settings;
using NUnit.Framework;

namespace EventStore.Core.Tests.Settings {
	[TestFixture]
	public class ReaderThreadCountCalculatorTests {
		const ulong GigaByte = CacheSizeCalculator.Gigabyte;

		[TestCase]
		public void configured_takes_precedence() => Test(configuredCount: 1, mem: GigaByte, expected: 1);

		[TestCase]
		public void enforces_minimum() => Test(configuredCount: 0, mem: 200, expected: 4);

		[TestCase]
		public void enforces_maximum() => Test(configuredCount: 0, mem: 128 * GigaByte, expected: 16);

		[TestCase]
		public void at_001gib() => Test(configuredCount: 0, mem: 1 * GigaByte, expected: 4);

		[TestCase]
		public void at_004gib() => Test(configuredCount: 0, mem: 4 * GigaByte, expected: 8);

		[TestCase]
		public void at_008gib() => Test(configuredCount: 0, mem: 8 * GigaByte, expected: 16);

		public static void Test(int configuredCount, ulong mem, int expected) =>
			Assert.AreEqual(expected, ThreadCountCalculator.CalculateReaderThreadCount(configuredCount, mem));
	}
}
