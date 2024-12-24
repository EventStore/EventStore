// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Configuration.Sources;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Configuration;

public class KurrentEnvironmentVariablesSourceTests {
	[Theory]
	[InlineData("KURRENT_STREAM_INFO_CACHE_CAPACITY", "Kurrent:StreamInfoCacheCapacity")]
	public void AddsKurrentEnvVars(string key, string normalizedKey) {
		// Arrange
		var environment = new Dictionary<string, string> { { key, key } };

		// Act
		var configuration = new ConfigurationBuilder()
			.AddKurrentEnvironmentVariables(environment)
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
	[InlineData("Kurrent:Cluster:StreamInfoCacheCapacity")]
	[InlineData("UNSUPPORTED_KURRENT_TCP_API_ENABLED")]
	public void IgnoresOtherEnvVars(string key) {
		// Arrange
		var environment = new Dictionary<string, string> { { key, key } };

		// Act
		var configuration = new ConfigurationBuilder()
			.AddKurrentEnvironmentVariables(environment)
			.Build();

		// Assert
		configuration.AsEnumerable()
			.Any().Should().BeFalse();
	}
}

public class FallbackEnvironmentVariablesSourceTests {
	[Theory]
	[InlineData("EVENTSTORE_STREAM_INFO_CACHE_CAPACITY", "Kurrent:StreamInfoCacheCapacity")]
	public void TransformsEventStoreEnvVars(string key, string normalizedKey) {
		// Arrange
		var environment = new Dictionary<string, string> { { key, key } };

		// Act
		var configuration = new ConfigurationBuilder()
			.AddFallbackEnvironmentVariables(environment)
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
	[InlineData("UNSUPPORTED_EVENTSTORE_TCP_API_ENABLED")]
	public void IgnoresOtherEnvVars(string key) {
		// Arrange
		var environment = new Dictionary<string, string> { { key, key } };

		// Act
		var configuration = new ConfigurationBuilder()
			.AddFallbackEnvironmentVariables(environment)
			.Build();

		// Assert
		configuration.AsEnumerable()
			.Any().Should().BeFalse();
	}
}
