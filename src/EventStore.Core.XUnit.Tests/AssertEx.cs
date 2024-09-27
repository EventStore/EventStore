using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

namespace EventStore.Core.XUnit.Tests;

public static class AssertEx {
	public static void IsOrBecomesTrue(
		Func<bool> func,
		TimeSpan? timeout = null,
		string msg = "AssertEx.IsOrBecomesTrue() timed out",
		[CallerMemberName] string memberName = "",
		[CallerFilePath] string sourceFilePath = "",
		[CallerLineNumber] int sourceLineNumber = 0) {

		Assert.True(
			SpinWait.SpinUntil(func, timeout ?? TimeSpan.FromMilliseconds(1000)),
			$"{msg} in {memberName} {sourceFilePath}:{sourceLineNumber}");
	}
}
