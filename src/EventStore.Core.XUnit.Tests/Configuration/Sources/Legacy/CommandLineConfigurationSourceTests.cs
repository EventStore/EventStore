// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Configuration.Sources;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Configuration.Sources.Legacy;

public class CommandLineConfigurationSourceTests {
	[Theory]
	[InlineData(new[] { "--stream_info_cache_capacity=99" }, "KurrentDB:StreamInfoCacheCapacity", "")]
	[InlineData(new[] { "--stream_info_cache_capacity", "99" }, "KurrentDB:StreamInfoCacheCapacity", "")]
	[InlineData(new[] { "--stream-info-cache-capacity=99" }, "KurrentDB:StreamInfoCacheCapacity", "")]
	[InlineData(new[] { "--stream-info-cache-capacity", "99" }, "KurrentDB:StreamInfoCacheCapacity", "")]
	[InlineData(new[] { "--EventStore:StreamInfoCacheCapacity=99" }, "KurrentDB:StreamInfoCacheCapacity", "99")]
	[InlineData(new[] { "--EventStore:StreamInfoCacheCapacity", "99" }, "KurrentDB:StreamInfoCacheCapacity", "99")]
	[InlineData(new[] { "--EventStore:Stream-Info-Cache-Capacity=99" }, "KurrentDB:StreamInfoCacheCapacity", "99")]
	[InlineData(new[] { "--EventStore:Stream-Info-Cache-Capacity", "99" }, "KurrentDB:StreamInfoCacheCapacity", "99")]
	[InlineData(new[] { "--EventStore:Stream_Info_Cache_Capacity=99" }, "KurrentDB:StreamInfoCacheCapacity", "99")]
	[InlineData(new[] { "--EventStore:Stream_Info_Cache_Capacity", "99" }, "KurrentDB:StreamInfoCacheCapacity", "99")]
	public void AddsEventStoreArguments(string[] arguments, string normalizedKey, string expectedValue) {
		// Act
		var configuration = new ConfigurationBuilder()
			.AddLegacyEventStoreCommandLine(arguments)
			.Build();

		// Assert
		if (string.IsNullOrEmpty(expectedValue)) {
			configuration.GetValue<string>(normalizedKey).Should().BeNull();
		} else {
			configuration.GetValue<string>(normalizedKey).Should().Be(expectedValue);
		}
	}
}