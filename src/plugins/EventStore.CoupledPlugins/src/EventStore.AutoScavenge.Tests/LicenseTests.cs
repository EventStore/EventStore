// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using EventStore.Plugins.Licensing;

namespace EventStore.AutoScavenge.Tests;

public class LicenseTests {
	[Fact]
	public void when_invalid_license_disable() {
		var startup = new DummyStartup(new ConfigurationBuilder().Build());
		var sut = startup.AutoScavengePlugin;

		var builder = WebApplication.CreateBuilder();

		startup.ConfigureServices(builder.Services);
		startup.Configure(builder.Build());
		Assert.True(sut.Enabled);

		var tokenWithoutEntitlement = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJlc2RiIiwiaXNzIjoiZXNkYiIsImV4cCI6MTgyMzA2NDUyNCwianRpIjoiYWFmODRlYmMtMjgzOS00MTYyLWE3NjUtODRjM2MyYzE3MWYyIiwic3ViIjoiRVNEQiBUZXN0cyIsIklzVHJpYWwiOiJUcnVlIiwiSXNFeHBpcmVkIjoiRmFsc2UiLCJJc1ZhbGlkIjoiVHJ1ZSIsIklzRmxvYXRpbmciOiJUcnVlIiwiRGF5c1JlbWFpbmluZyI6IjEiLCJTdGFydERhdGUiOiIyNi8wNC8yMDI0IDAwOjAwOjAwICswMTowMCIsIk5PTkUiOiJ0cnVlIiwiaWF0IjoxNzI4NDU2NTI0LCJuYmYiOjE3Mjg0NTY1MjR9.whHm0KeqhM3g2SR2MN6-EdgpA9N5-USu4jxdMchdOhrl_Shf10t83BUTHrYCwXXtGvqgWTIPqU-sCnw7uLww5g";
		startup.LicenseService.EmitLicense(new License(new(tokenWithoutEntitlement)));
		Assert.False(sut.Enabled);
	}

	[Fact]
	public void when_valid_license_stay_enabled() {
		var startup = new DummyStartup(new ConfigurationBuilder().Build());
		var sut = startup.AutoScavengePlugin;

		var builder = WebApplication.CreateBuilder();

		startup.ConfigureServices(builder.Services);
		startup.Configure(builder.Build());
		Assert.True(sut.Enabled);

		startup.LicenseService.EmitLicense(startup.LicenseService.CurrentLicense!);
		Assert.True(sut.Enabled);
	}
}
