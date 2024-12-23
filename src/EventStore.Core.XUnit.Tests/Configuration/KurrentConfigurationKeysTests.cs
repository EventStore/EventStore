// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

// ReSharper disable CheckNamespace

using EventStore.Core.Configuration.Sources;
using FluentAssertions;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Configuration;

public class KurrentConfigurationKeysTests {
	[Theory]
	[InlineData("EVENTSTORE_StreamInfoCacheCapacity", "EventStore:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE_STREAM_INFO_CACHE_CAPACITY", "EventStore:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE__STREAM_INFO_CACHE_CAPACITY", "EventStore:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE__CLUSTER__STREAM_INFO_CACHE_CAPACITY", "EventStore:Cluster:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE__KEY_WITH_UNDERSCORES__ANOTHER_ONE", "EventStore:KeyWithUnderscores:AnotherOne")]
	[InlineData("StreamInfoCacheCapacity", "EventStore:StreamInfoCacheCapacity")]
	[InlineData("stream-info-cache-capacity", "EventStore:StreamInfoCacheCapacity")]
	[InlineData("EventStore:Cluster:StreamInfoCacheCapacity", "EventStore:Cluster:StreamInfoCacheCapacity")]
	[InlineData("GossipSeed", "EventStore:GossipSeed")]
	[InlineData("EVENTSTORE_GOSSIP_SEED", "EventStore:GossipSeed")]
	[InlineData("EVENTSTORE_GOSSIPSEED", "EventStore:GossipSeed")]
	public void NormalizesEventStoreKeys(string key, string normalizedKey) {
		KurrentConfigurationKeys.Normalize(key).Should().Be(normalizedKey);
	}
}
