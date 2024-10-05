// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.TransactionLog.Scavenging;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Scavenge;

public class StreamHandleTests {
	[Fact]
	public void equals_int() {
		Assert.Equal(new StreamHandle<int>(), new StreamHandle<int>());
		Assert.Equal(StreamHandle.ForStreamId(5), StreamHandle.ForStreamId(5));
		Assert.Equal(StreamHandle.ForHash<int>(5), StreamHandle.ForHash<int>(5));

		Assert.NotEqual(StreamHandle.ForStreamId(5), StreamHandle.ForStreamId(6));
		Assert.NotEqual(StreamHandle.ForHash<int>(5), StreamHandle.ForHash<int>(6));

		Assert.NotEqual(StreamHandle.ForStreamId(5), StreamHandle.ForHash<int>(5));

		Assert.NotEqual(new StreamHandle<int>(), StreamHandle.ForHash<int>(5));
		Assert.NotEqual(new StreamHandle<int>(), StreamHandle.ForStreamId(5));
	}

	[Fact]
	public void equals_string() {
		Assert.Equal(new StreamHandle<string>(), new StreamHandle<string>());
		Assert.Equal(StreamHandle.ForStreamId("5"), StreamHandle.ForStreamId("5"));
		Assert.Equal(StreamHandle.ForHash<string>(5), StreamHandle.ForHash<string>(5));

		Assert.NotEqual(StreamHandle.ForStreamId("5"), StreamHandle.ForStreamId("6"));
		Assert.NotEqual(StreamHandle.ForHash<string>(5), StreamHandle.ForHash<string>(6));

		Assert.NotEqual(StreamHandle.ForStreamId("5"), StreamHandle.ForHash<string>(5));

		Assert.NotEqual(new StreamHandle<string>(), StreamHandle.ForHash<string>(5));
		Assert.NotEqual(new StreamHandle<string>(), StreamHandle.ForStreamId("5"));
	}
}
