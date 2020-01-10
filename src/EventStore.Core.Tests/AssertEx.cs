using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using KellermanSoftware.CompareNetObjects;
using NUnit.Framework;

namespace EventStore.Core.Tests {
	public static class AssertEx {
		private static ComparisonConfig Configuration = new ComparisonConfig {
			IgnoreCollectionOrder = false,
			MaxDifferences = 10,
		};
		public static void AssertUsingDeepCompare(object actual, object expected) {
			var compareLogic = new CompareLogic(Configuration);
			var result = compareLogic.Compare(actual, expected);
			Assert.IsTrue(result.AreEqual, string.Join(Environment.NewLine, result.Differences.Select(x =>
				$"{x.GetWhatIsCompared()}, Values ({FormatString(x.Object1)},{FormatString(x.Object2)})")));
		}

		private static string FormatString(object o) {
			if (o == null) return string.Empty;
			if (o is DateTime time) {
				return time.ToString("MM/dd/yyyy HH:mm:ss.fff",
					CultureInfo.InvariantCulture);
			}

			return o.ToString();
		}
		
		public static async Task<TException> ThrowsAsync<TException>(Func<Task> code)
			where TException : Exception {
			var expected = default(TException);
			try {
				await code();
				Assert.Fail($"Expected exception of type: {typeof(TException)}");
			} catch (TException ex) {
				expected = ex;
			} catch (Exception ex) {
				Assert.Fail($"Expected exception of type: {typeof(TException)} but was {ex.GetType()} instead");
			}

			return expected;
		}

		public static async Task DoesNotThrowAsync<TException>(Func<Task> code, string message)
			where TException : Exception {
			try {
				await code();
			} catch (TException) {
				Assert.Fail(message);
			}
		}
	}
}
