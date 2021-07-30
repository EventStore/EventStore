using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace EventStore.Core.XUnit.Tests {
	public class AssertEx {
		public static void IsOrBecomesTrue(
			Func<bool> func,
			TimeSpan? timeout = null,
			string msg = "AssertEx.IsOrBecomesTrue() timed out",
			bool yieldThread = false,
			[CallerMemberName] string memberName = "",
			[CallerFilePath] string sourceFilePath = "",
			[CallerLineNumber] int sourceLineNumber = 0) {

			Assert.True(
				EventStore.Core.Tests.AssertEx.IsOrBecomesTrueImpl(func, timeout, yieldThread),
				$"{msg} in {memberName} {sourceFilePath}:{sourceLineNumber}");
		}
	}
}
