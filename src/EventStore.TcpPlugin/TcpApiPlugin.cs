// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Configuration.Sources;
using EventStore.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventStore.TcpPlugin;

public class TcpApiPlugin : SubsystemsPlugin {
	public TcpApiPlugin() : base(requiredEntitlements: ["TCP_CLIENT_PLUGIN"]) {
	}

	public override (bool Enabled, string EnableInstructions) IsEnabled(IConfiguration configuration) {
		var enabled = configuration.GetValue($"{KurrentConfigurationKeys.Prefix}:TcpPlugin:EnableExternalTcp", defaultValue: false);
		return (enabled, $"Set '{KurrentConfigurationKeys.Prefix}:TcpPlugin:EnableExternalTcp' to 'true' to enable");
	}

	public override void ConfigureServices(IServiceCollection services, IConfiguration configuration) {
		var options = configuration.GetSection(KurrentConfigurationKeys.Prefix).Get<EventStoreOptions>() ?? new();
		services.AddSingleton(options);
		services.AddHostedService<PublicTcpApiService>();
	}
}
