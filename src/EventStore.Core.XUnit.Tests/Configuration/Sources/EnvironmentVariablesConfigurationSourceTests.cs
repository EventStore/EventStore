using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Configuration.Sources;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Configuration {
	public class EventStoreEnvironmentVariablesSourceTests {
		[Theory]
		[InlineData("EVENTSTORE_STREAM_INFO_CACHE_CAPACITY", "EventStore:StreamInfoCacheCapacity")]
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
		[InlineData("EVENTSTORE__PLUGINS__FOO_BAR")] // the normal environment provider will provide this differently so we dont want to
		[InlineData("UNSUPPORTED_EVENTSTORE_TCP_API_ENABLED")]
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
