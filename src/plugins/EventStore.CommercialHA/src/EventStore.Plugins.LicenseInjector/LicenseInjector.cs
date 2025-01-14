// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Plugins.Licensing;
using EventStore.Plugins.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventStore.Plugins.LicenseInjector;

public class LicenseInjector : SubsystemsPlugin {
	public override void ConfigureServices(IServiceCollection services, IConfiguration configuration) {
		services.AddSingleton<ILicenseService>(new Fixtures.FakeLicenseService());
	}
}
