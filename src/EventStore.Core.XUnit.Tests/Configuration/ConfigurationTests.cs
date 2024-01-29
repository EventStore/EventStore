// ReSharper disable CheckNamespace

using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Configuration.Sources;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventStore.Core.Tests.Configuration;

public class ConfigurationTests {
	[Theory]
	[InlineData("EVENTSTORE_STREAM_INFO_CACHE_CAPACITY", "EventStore:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE__STREAM_INFO_CACHE_CAPACITY", "EventStore:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE__CLUSTER__STREAM_INFO_CACHE_CAPACITY", "EventStore:Cluster:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE__KEY_WITH_UNDERSCORES__ANOTHER_ONE", "EventStore:KeyWithUnderscores:AnotherOne")]
	[InlineData("StreamInfoCacheCapacity", "EventStore:StreamInfoCacheCapacity")]
	[InlineData("stream-info-cache-capacity", "EventStore:StreamInfoCacheCapacity")]
	[InlineData("EventStore:Cluster:StreamInfoCacheCapacity", "EventStore:Cluster:StreamInfoCacheCapacity")]
	public void NormalizesEventStoreKeys(string key, string normalizedKey) {
		// Act
		var result = EventStoreConfigurationKeys.Normalize(key);
		
		// Assert
		result.Should().BeEquivalentTo(normalizedKey);
	}
}

public class EventStoreEnvironmentVariablesSourceTests {
	[Theory]
	[InlineData("EVENTSTORE_STREAM_INFO_CACHE_CAPACITY", "EventStore:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE__STREAM_INFO_CACHE_CAPACITY", "EventStore:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE__CLUSTER__STREAM_INFO_CACHE_CAPACITY", "EventStore:Cluster:StreamInfoCacheCapacity")]
	[InlineData("EVENTSTORE__KEY_WITH_UNDERSCORES__ANOTHER_ONE", "EventStore:KeyWithUnderscores:AnotherOne")]
	public void AddsEventStoreEnvVars(string key, string normalizedKey) {
		// Arrange
		var environment = new Dictionary<string, string> { { key, key } };
		
		// Act
		var configuration = new ConfigurationBuilder()
			.AddEventStoreEnvironmentVariables(environment)
			.Build();
		
		// Assert
		configuration
			.AsEnumerable()
			.Any(x => x.Key == normalizedKey)
			.Should().BeTrue();
	}
	
	[Theory]
	[InlineData("StreamInfoCacheCapacity")]
	[InlineData("stream-info-cache-capacity")]
	[InlineData("EventStore:Cluster:StreamInfoCacheCapacity")]
	public void IgnoresOtherEnvVars(string key) {
		// Arrange
		var environment = new Dictionary<string, string> { { key, key } };
		
		// Act
		var configuration = new ConfigurationBuilder()
			.AddEventStoreEnvironmentVariables(environment)
			.Build();
		
		// Assert
		configuration.AsEnumerable()
			.Any().Should().BeFalse();
	}
}


public class EventStoreCommandLineSourceTests {
	[Theory]
	[InlineData("--stream_info_cache_capacity=99", "EventStore:StreamInfoCacheCapacity", "99")]
	public void AddsArguments(string argument, string normalizedKey, string expectedValue) {
		// Act
		var configuration = new ConfigurationBuilder()
			.AddEventStoreCommandLine([argument])
			.Build();
		
		// Assert
		configuration.GetValue<string>(normalizedKey).Should().Be(expectedValue);
	}
}


public class EventStoreDefaultValuesConfigurationSourceTests {
	[Fact]
	public void Adds() {
		// Arrange
		var defaults = ClusterVNodeOptions.DefaultValues.OrderBy(x => x.Key).ToList();
		
		// Act
		var configuration = new ConfigurationBuilder()
			.AddEventStoreDefaultValues(defaults)
			.Build()
			.GetSection(EventStoreConfigurationKeys.Prefix);
		
		// Assert
		foreach (var (key, value) in defaults) {
			configuration.GetValue<object>(key)
				?.ToString()
				.Should()
				.BeEquivalentTo(value?.ToString(), $"because {key} should be {value}");
		}
	}
}
