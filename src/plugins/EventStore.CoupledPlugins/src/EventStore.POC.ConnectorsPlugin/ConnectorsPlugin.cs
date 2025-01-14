// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Plugins;
using EventStore.Plugins.Diagnostics;
using EventStore.Plugins.Subsystems;
using EventStore.POC.ConnectorsEngine;
using EventStore.POC.ConnectorsEngine.Processing;
using EventStore.POC.IO.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace EventStore.POC.ConnectorsPlugin;

// TODO: ConnectorsPlugin cannot implement ISubsystem directly because it forms a diamond
// inheritance on IPlugableComponent which doesn't play nicely with the explicit implemention
// in the Plugin class. The result is that the Plugin's implementation of ConfigureApplication
// is not called, it goes straight to the derived. We should rename it to ConfigureApplicationWhenEnabled
// and then all this will go away.
public class ConnectorsPluginBase(string version, string name) : Plugin(version: version, name: name), ISubsystem {
	public virtual Task Start() => Task.CompletedTask;

	public virtual Task Stop() => Task.CompletedTask;
}

public class ConnectorsPlugin : ConnectorsPluginBase, IConnectedSubsystemsPlugin {
	private static readonly ILogger _logger = Log.ForContext<ConnectorsPlugin>();

	public ConnectorsPlugin() : base(version: "0.0.5", name: "ConnectorsPreview") {
	}

	public string CommandLineName => "connectors-preview";

	public IReadOnlyList<ISubsystem> GetSubsystems() => [this];

	private Engine? _engine;
	private CancellationTokenSource _cancellationTokenSource = new();

	public override (bool Enabled, string EnableInstructions) IsEnabled(IConfiguration configuration) {
		var enabled = configuration.GetValue("EventStore:Plugins:Connectors:Enabled", defaultValue: false);
		return (enabled, "Set 'EventStore:Plugins:Connectors:Enabled' to 'true' to enable ConnectorsPreview");
	}

	public override void ConfigureApplication(IApplicationBuilder builder, IConfiguration configuration) {
		builder
			.UseEndpoints(ep => {
				//qq move to the server or plugin?
				//qq check authentication
				ep.MapControllers();
			});

		_engine = builder.ApplicationServices.GetRequiredService<Engine>();
	}

	public override void ConfigureServices(IServiceCollection services, IConfiguration config) {
		var mvc = services.AddControllers()
			.AddJsonOptions(x => {
				x.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
			});
		// register the controllers
		//qq move to the server or plugin?
		mvc.AddApplicationPart(typeof(Engine).Assembly);

		services.AddSingleton<Engine>();
	}

	private async Task CollectTelemetryLoop(CancellationToken ct) {
		while (true) {
			await Task.Delay(5000, ct);
			await CollectTelemetry();
		}
	}

	public async Task CollectTelemetry() {
		try {
			var result = await TelemetryReporter
				.CollectTelemetryAsync(
					Enabled,
					() => _engine?.ReadSide.ListAsync() ?? Task.FromResult<ConnectorState[]>([]),
					() => _engine?.ReadSide.ListActiveConnectorsAsync() ?? Task.FromResult<string[]>([]));
			PublishDiagnosticsData(result, PluginDiagnosticsDataCollectionMode.Snapshot);
		} catch (Exception ex) {
			 _logger.Warning(ex, "Could not collect telemetry from connectors engine");
		}
	}

	public override Task Start() {
		if (_engine is null)
			return Task.CompletedTask;

		var startTask = _engine.Start();

		_ = CollectTelemetryLoop(_cancellationTokenSource.Token);

		return startTask;
	}

	public override Task Stop() {
		if (_engine is null)
			return Task.CompletedTask;

		_cancellationTokenSource.Cancel();
		_cancellationTokenSource = new();
		return _engine.Stop();
	}
}
