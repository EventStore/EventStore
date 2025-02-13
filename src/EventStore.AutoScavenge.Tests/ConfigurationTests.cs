// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Configuration.Sources;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace EventStore.AutoScavenge.Tests;

public class ConfigurationTests {
	public enum EnabledStatus {
		True,
		False,
		Exception,
	}

	[Theory]
	[InlineData("true", "true", EnabledStatus.Exception)]
	[InlineData("true", "false", EnabledStatus.True)]
	[InlineData("", "true", EnabledStatus.False)]
	[InlineData("", "false", EnabledStatus.True)]
	[InlineData("false", "true", EnabledStatus.False)]
	[InlineData("false", "false", EnabledStatus.False)]
	public void enables_correctly_according_to_configuration(string enabledValue, string devModeValue, EnabledStatus expected) {
		var startup = new DummyStartup(
			new ConfigurationBuilder()
				.AddInMemoryCollection([
					new($"{KurrentConfigurationKeys.Prefix}:AutoScavenge:Enabled", enabledValue),
					new($"{KurrentConfigurationKeys.Prefix}:Dev", devModeValue),
				])
				.Build());

		var sut = startup.AutoScavengePlugin;

		var builder = WebApplication.CreateBuilder();

		void When() {
			startup.ConfigureServices(builder.Services);
			startup.Configure(builder.Build());
		}

		if (expected == EnabledStatus.Exception) {
			Assert.Throws<Exception>(When);
		} else {
			When();

			if (expected == EnabledStatus.True) {
				Assert.True(sut.Enabled);
			} else if (expected == EnabledStatus.False) {
				Assert.False(sut.Enabled);
			}
		}
	}
}
