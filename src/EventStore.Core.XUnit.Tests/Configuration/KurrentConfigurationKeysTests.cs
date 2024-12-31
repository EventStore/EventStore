// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

// ReSharper disable CheckNamespace

using EventStore.Core.Configuration.Sources;
using FluentAssertions;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Configuration;

public class KurrentConfigurationKeysTests {
	[Theory]
	[InlineData("KURRENT_StreamInfoCacheCapacity", "Kurrent:StreamInfoCacheCapacity")]
	[InlineData("KURRENT_STREAM_INFO_CACHE_CAPACITY", "Kurrent:StreamInfoCacheCapacity")]
	[InlineData("KURRENT__STREAM_INFO_CACHE_CAPACITY", "Kurrent:StreamInfoCacheCapacity")]
	[InlineData("KURRENT__CLUSTER__STREAM_INFO_CACHE_CAPACITY", "Kurrent:Cluster:StreamInfoCacheCapacity")]
	[InlineData("KURRENT__KEY_WITH_UNDERSCORES__ANOTHER_ONE", "Kurrent:KeyWithUnderscores:AnotherOne")]
	[InlineData("StreamInfoCacheCapacity", "Kurrent:StreamInfoCacheCapacity")]
	[InlineData("stream-info-cache-capacity", "Kurrent:StreamInfoCacheCapacity")]
	[InlineData("Kurrent:Cluster:StreamInfoCacheCapacity", "Kurrent:Cluster:StreamInfoCacheCapacity")]
	[InlineData("GossipSeed", "Kurrent:GossipSeed")]
	[InlineData("KURRENT_GOSSIP_SEED", "Kurrent:GossipSeed")]
	[InlineData("KURRENT_GOSSIPSEED", "Kurrent:GossipSeed")]
	public void NormalizesKurrentKeys(string key, string normalizedKey) {
		KurrentConfigurationKeys.Normalize(key).Should().Be(normalizedKey);
	}

	[Theory]
	[InlineData("EVENTSTORE_StreamInfoCacheCapacity", "Kurrent:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE_STREAM_INFO_CACHE_CAPACITY", "Kurrent:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE__STREAM_INFO_CACHE_CAPACITY", "Kurrent:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE__CLUSTER__STREAM_INFO_CACHE_CAPACITY", "Kurrent:Cluster:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE__KEY_WITH_UNDERSCORES__ANOTHER_ONE", "Kurrent:KeyWithUnderscores:AnotherOne")]
	[InlineData("StreamInfoCacheCapacity", "Kurrent:StreamInfoCacheCapacity")]
	[InlineData("stream-info-cache-capacity", "Kurrent:StreamInfoCacheCapacity")]
	[InlineData("EventStore:Cluster:StreamInfoCacheCapacity", "Kurrent:Cluster:StreamInfoCacheCapacity")]
	[InlineData("GossipSeed", "Kurrent:GossipSeed")]
	[InlineData("EVENTSTORE_GOSSIP_SEED", "Kurrent:GossipSeed")]
	[InlineData("EVENTSTORE_GOSSIPSEED", "Kurrent:GossipSeed")]
	public void TransformsEventStoreEnvVars(string key, string normalizedKey) {
		KurrentConfigurationKeys.Normalize(
			KurrentConfigurationKeys.LegacyEventStorePrefix, KurrentConfigurationKeys.Prefix, key).Should().Be(normalizedKey);
	}
}
