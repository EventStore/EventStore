// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Json.Nodes;
using EventStore.Plugins;
using EventStore.Plugins.Diagnostics;
using EventStore.POC.ConnectorsPlugin;
using EventStore.POC.IO.Core;
using FluentAssertions;
using Json.More;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Eventstore.POC.Tests;

public class ConnectorsPluginTests {
	static ConnectorsPlugin CreateSut(bool enabled) {
		using var sut = new ConnectorsPlugin();

		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> {
				{"EventStore:Plugins:Connectors:Enabled", $"{enabled}"},
			})
			.Build();

		var builder = WebApplication.CreateBuilder();
		builder.Services.AddSingleton<IClient>(new IClient.None());

		((IPlugableComponent)sut).ConfigureServices(
			builder.Services,
			config);

		var app = builder.Build();
		app.UseRouting();
		((IPlugableComponent)sut).ConfigureApplication(app, config);
		return sut;
	}

	[Fact]
	public async Task CanCollectTelemetryWhenDisabled() {
		// given
		using var collector = PluginDiagnosticsDataCollector.Start("ConnectorsPreview");
		using var sut = CreateSut(enabled: false);

		await sut.Start();

		// when
		await sut.CollectTelemetry();

		// then
		collector.CollectedEvents("ConnectorsPreview").Should().ContainSingle().Which
			.Data["enabled"].Should().Be(false);

		await sut.Stop();
	}

	[Fact]
	public async Task CanCollectTelemetryWhenEnabled() {
		// given
		using var collector = PluginDiagnosticsDataCollector.Start("ConnectorsPreview");
		using var sut = CreateSut(enabled: true);

		await sut.Start();

		// when
		await sut.CollectTelemetry();

		// then
		var data = collector.CollectedEvents("ConnectorsPreview").Should().ContainSingle().Which.Data;
		data.Should().Equal(new Dictionary<string, object?>() {
			{ "enabled", true },
			{ "connectorsActiveOnNode", 0 },
			{ "connectorsEnabledTotal", 0 },
		});

		await sut.Stop();
	}
}
