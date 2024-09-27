// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

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
