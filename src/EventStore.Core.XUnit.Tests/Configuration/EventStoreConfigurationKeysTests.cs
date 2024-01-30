// ReSharper disable CheckNamespace

using EventStore.Core.Configuration.Sources;
using FluentAssertions;
using Xunit;

namespace EventStore.Core.Tests.Configuration;

public class EventStoreConfigurationKeysTests {
	[Theory]
	[InlineData("EVENTSTORE_STREAM_INFO_CACHE_CAPACITY", "EventStore:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE__STREAM_INFO_CACHE_CAPACITY", "EventStore:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE__CLUSTER__STREAM_INFO_CACHE_CAPACITY", "EventStore:Cluster:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE__KEY_WITH_UNDERSCORES__ANOTHER_ONE", "EventStore:KeyWithUnderscores:AnotherOne")]
	[InlineData("StreamInfoCacheCapacity", "EventStore:StreamInfoCacheCapacity")]
	[InlineData("stream-info-cache-capacity", "EventStore:StreamInfoCacheCapacity")]
	[InlineData("EventStore:Cluster:StreamInfoCacheCapacity", "EventStore:Cluster:StreamInfoCacheCapacity")]
	public void NormalizesEventStoreKeys(string key, string normalizedKey) {
		EventStoreConfigurationKeys.Normalize(key).Should().BeEquivalentTo(normalizedKey);
	}
}

