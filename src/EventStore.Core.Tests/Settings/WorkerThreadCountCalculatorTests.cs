using EventStore.Core.Settings;
using NUnit.Framework;

namespace EventStore.Core.Tests.Settings {
	[TestFixture]
	public class WorkerThreadCountCalculatorTests {
		[TestCase]
		public void configured_takes_precedence() => Test(configuredCount: 1, readerCount: 10, expected: 1);

		[TestCase]
		public void enforces_minimum() => Test(configuredCount: 0, readerCount: 1, expected: 5);

		[TestCase]
		public void enforces_maximum() => Test(configuredCount: 0, readerCount: 1000, expected: 10);

		[TestCase]
		public void at_4_reader_threads() => Test(configuredCount: 0, 4, expected: 5);

		[TestCase]
		public void at_increased_reader_threads() => Test(configuredCount: 0, 8, expected: 10);

		public static void Test(int configuredCount, int readerCount, int expected) =>
			Assert.AreEqual(expected, ThreadCountCalculator.CalculateWorkerThreadCount(configuredCount, readerCount));

	}
}
