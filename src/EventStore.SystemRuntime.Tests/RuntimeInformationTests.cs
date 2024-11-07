// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Runtime;
using Xunit;

namespace EventStore.SystemRuntime.Tests;

public class RuntimeInformationTests {
	[Fact]
	public void can_get_runtime_version() {
		Assert.NotNull(RuntimeInformation.RuntimeVersion);
	}

	[Fact]
	public void can_get_runtime_mode() {
		Assert.True(RuntimeInformation.RuntimeMode > 0);
	}
}
