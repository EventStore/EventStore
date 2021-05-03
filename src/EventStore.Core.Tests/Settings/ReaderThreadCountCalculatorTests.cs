using EventStore.Core.Settings;
using NUnit.Framework;

namespace EventStore.Core.Tests.Settings {
	[TestFixture]
	public class ReaderThreadCountCalculatorTests {
		[TestCase]
		public void configured_takes_precedence() => Test(configuredCount: 1, processorCount: 4, expected: 1);

		[TestCase]
		public void enforces_minimum() => Test(configuredCount: 0, processorCount: 1, expected: 4);

		[TestCase]
		public void enforces_maximum() => Test(configuredCount: 0, processorCount: 20, expected: 16);

		[TestCase]
		public void at_3_cores() => Test(configuredCount: 0, processorCount: 3, expected: 6);

		[TestCase]
		public void at_4_cores() => Test(configuredCount: 0, processorCount: 4, expected: 8);

		[TestCase]
		public void at_6_cores() => Test(configuredCount: 0, processorCount: 6, expected: 12);

		public static void Test(int configuredCount, int processorCount, int expected) =>
			Assert.AreEqual(expected, ThreadCountCalculator.CalculateReaderThreadCount(configuredCount, processorCount));
	}
}
