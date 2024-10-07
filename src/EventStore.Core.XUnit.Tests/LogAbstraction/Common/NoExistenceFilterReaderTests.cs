// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.LogAbstraction.Common;
using Xunit;

namespace EventStore.Core.XUnit.Tests.LogAbstraction.Common;

public class NoExistenceFilterReaderTests {
	[Fact]
	public void might_contain_anything() {
		var sut = new NoExistenceFilterReader();
		Assert.True(sut.MightContain(4)); // chosen by fair dice roll
	}
}
