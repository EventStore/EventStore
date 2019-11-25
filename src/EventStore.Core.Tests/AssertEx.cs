using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace EventStore.Core.Tests {
	public static class AssertEx {
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
