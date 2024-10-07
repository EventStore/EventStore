// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index;

[TestFixture]
public class ReverseComparerTests {
	[Test]
	public void larger_values_return_as_lower() {
		Assert.AreEqual(-1, new ReverseComparer<int>().Compare(5, 3));
	}

	[Test]
	public void smaller_values_return_as_higher() {
		Assert.AreEqual(1, new ReverseComparer<int>().Compare(3, 5));
	}

	[Test]
	public void same_values_are_equal() {
		Assert.AreEqual(0, new ReverseComparer<int>().Compare(5, 5));
	}
}
