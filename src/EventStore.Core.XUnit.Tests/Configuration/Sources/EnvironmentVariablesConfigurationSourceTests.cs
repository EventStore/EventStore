using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Configuration.Sources;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventStore.Core.Tests.Configuration {
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
}
