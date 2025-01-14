// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventStore.TcpUnitTestPlugin;

public class TcpApiTestPlugin : SubsystemsPlugin {
	public override void ConfigureServices(IServiceCollection services, IConfiguration configuration) {
		var options = configuration.GetSection("EventStore:TcpUnitTestPlugin").Get<TcpTestOptions>() ?? new();
		services.AddSingleton(options);
		services.AddHostedService<PublicTcpApiTestService>();
	}
}
