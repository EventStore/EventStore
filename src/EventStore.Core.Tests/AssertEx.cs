using System;
using System.Threading;
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
		  /// <summary>
        /// Asserts the given function will return false before the timeout expires.
        /// Repeatedly evaluates the function until false is returned or the timeout expires.
        /// Will return immediately when the condition is false.
        /// Evaluates the timeout every 10 msec until expired.
        /// Will not yield the thread by default, if yielding is required to resolve deadlocks set yieldThread to true.
        /// </summary>
        /// <param name="func">The function to evaluate.</param>
        /// <param name="timeout">A timeout in milliseconds. If not specified, defaults to 1000.</param>
        /// <param name="msg">A message to display if the condition is not satisfied.</param>
        /// <param name="yieldThread">If true, the thread relinquishes the remainder of its time
        /// slice to any thread of equal priority that is ready to run.</param>
        public static void IsOrBecomesFalse(Func<bool> func, int? timeout = null, string msg = null, bool yieldThread = false)
        {
            IsOrBecomesTrue(() => !func(), timeout, msg, yieldThread);
        }

        /// <summary>
        /// Asserts the given function will return true before the timeout expires.
        /// Repeatedly evaluates the function until true is returned or the timeout expires.
        /// Will return immediately when the condition is true.
        /// Evaluates the timeout every 10 msec until expired.
        /// Will not yield the thread by default, if yielding is required to resolve deadlocks set yieldThread to true.
        /// </summary>
        /// <param name="func">The function to evaluate.</param>
        /// <param name="timeout">A timeout in milliseconds. If not specified, defaults to 1000.</param>
        /// <param name="msg">A message to display if the condition is not satisfied.</param>
        /// <param name="yieldThread">If true, the thread relinquishes the remainder of its time
        /// slice to any thread of equal priority that is ready to run.</param>
        public static void IsOrBecomesTrue(Func<bool> func, int? timeout = null, string msg = null, bool yieldThread = false)
        {
            if (yieldThread) Thread.Sleep(0);
            if (!timeout.HasValue) timeout = 1000;
            var waitLoops = timeout / 10;
            var result = false;
            for (int i = 0; i < waitLoops; i++) {                
                if (SpinWait.SpinUntil(func, 10)){
                    result = true;
                    break;
                }
            }
            Assert.True(result, msg ?? "");
        }
	}
}
