// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Json.Nodes;
using EventStore.Plugins;
using EventStore.Plugins.Diagnostics;
using EventStore.Plugins.Licensing;
using EventStore.Plugins.Tests;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventStore.MD5.Tests;

public class MD5ProviderTests {
	[Fact]
	public void can_collect_telemetry() {
		// given
		using var sut = new MD5Provider();
		using var collector = PluginDiagnosticsDataCollector.Start(sut.DiagnosticsName);

		var config = new ConfigurationBuilder().Build();
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddSingleton<ILicenseService>(new Fixtures.FakeLicenseService());

		// when
		((IPlugableComponent)sut).ConfigureServices(builder.Services, config);
		var app = builder.Build();
		((IPlugableComponent)sut).ConfigureApplication(app, config);

		// then
		collector.CollectedEvents(sut.DiagnosticsName).Should().ContainSingle().Which
			.Data["enabled"].Should().Be(true);
	}

	[Theory]
	[InlineData(true, "MD5", false)]
	[InlineData(true, "NONE", true)]
	[InlineData(false, "NONE", true)]
	public void respects_license(bool licensePresent, string entitlement, bool expectedException) {
		// given
		using var sut = new MD5Provider();

		var config = new ConfigurationBuilder().Build();
		var builder = WebApplication.CreateBuilder();

		var licenseService = new Fixtures.FakeLicenseService(licensePresent, entitlement);
		builder.Services.AddSingleton<ILicenseService>(licenseService);

		((IPlugableComponent)sut).ConfigureServices(
			builder.Services,
			config);

		var app = builder.Build();

		// when
		((IPlugableComponent)sut).ConfigureApplication(app, config);

		// then
		if (expectedException) {
			Assert.NotNull(licenseService.RejectionException);
		} else {
			Assert.Null(licenseService.RejectionException);
		}
	}
}
