// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.Subsystems;
using EventStore.PluginHosting;
using EventStore.Plugins;
using EventStore.Plugins.Subsystems;
using EventStore.POC.IO.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace EventStore.POC.ConnectedSubsystemsPlugin;

public class ConnectedSubsystemsPlugin : SubsystemsPlugin {
	private static readonly ILogger Log = Serilog.Log.ForContext<ConnectedSubsystemsPlugin>();

	public ConnectedSubsystemsPlugin() : base(version: "0.0.5", name: "ConnectedSubsystems") {
	}

	public override IReadOnlyList<ISubsystem> GetSubsystems() {
		var conntectedSubsystemsDir = new DirectoryInfo(Locations.PluginsDirectory);
		var pluginLoader = new PluginLoader(conntectedSubsystemsDir, typeof(IClient).Assembly);

		var plugins = pluginLoader.Load<IConnectedSubsystemsPlugin>();
		var subsystems = new List<ISubsystem> { this };
		foreach (var plugin in plugins) {
			Log.Information("Loaded ConnectedSubsystemsPlugin plugin: {plugin} {version}.",
				plugin.CommandLineName,
				plugin.Version);
			subsystems.AddRange(plugin.GetSubsystems());
		}

		return subsystems;
	}

	public override void ConfigureServices(IServiceCollection services, IConfiguration configuration) {
		services.AddSingleton<IClient, InternalClient>();
		services.AddSingleton<IOperationsClient, InternalOperationsClient>();
	}
}
