// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Reactive.Subjects;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http.NodeHttpClientFactory;
using EventStore.Plugins;
using EventStore.Plugins.Licensing;
using EventStore.POC.ConnectedSubsystemsPlugin;
using EventStore.POC.IO.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventStore.AutoScavenge.Tests;

public class DummyStartup {
	private const string LicenseToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJlc2RiIiwiaXNzIjoiZXNkYiIsImV4cCI6MTgyMzA2NDUyNCwianRpIjoiYjU2OGJlNWEtNjUwNy00ZWM5LTgzOWItMmE0ZjVlYzI5MTM5Iiwic3ViIjoiRVNEQiBUZXN0cyIsIklzVHJpYWwiOiJUcnVlIiwiSXNFeHBpcmVkIjoiRmFsc2UiLCJJc1ZhbGlkIjoiVHJ1ZSIsIklzRmxvYXRpbmciOiJUcnVlIiwiRGF5c1JlbWFpbmluZyI6IjEiLCJTdGFydERhdGUiOiIyNi8wNC8yMDI0IDAwOjAwOjAwICswMTowMCIsIkFVVE9fU0NBVkVOR0UiOiJ0cnVlIiwiaWF0IjoxNzI4NDU2NTI0LCJuYmYiOjE3Mjg0NTY1MjR9.oVcgaOLUmhuflw4IOLJq6kCQua1reg4-ZEUjCDvUSSDg23QM42eNe2Ug15tMTmiKON9IfQdxYLwHy_qo2A9q0g";
	public ConnectedSubsystemsPlugin ConnectedPlugin { get; init; } = new();
	public AutoScavengePlugin AutoScavengePlugin { get; init; } = new();
	public FakeLicenseService LicenseService { get; init; } = new FakeLicenseService(LicenseToken);

	public DummyStartup(IConfiguration configuration) {
		Configuration = configuration;
	}

	public IConfiguration Configuration { get; }

	public void ConfigureServices(IServiceCollection services) {
		services.AddControllers();
		services.AddAuthentication("dummy")
			.AddScheme<AuthenticationSchemeOptions, DummyAuthHandler>("dummy", null);

		services.AddSingleton<ILicenseService>(LicenseService);
		services.AddSingleton<INodeHttpClientFactory, DummyNodeHttpClientFactory>();
		services.AddSingleton<IClient, DummyClient>();
		services.AddSingleton<IOperationsClient, DummyOperationClient>();
		((IPlugableComponent)AutoScavengePlugin).ConfigureServices(services, Configuration);
		services.AddSingleton(AutoScavengePlugin);
	}

	public void Configure(IApplicationBuilder app) {
		app.UseRouting();
		app.UseAuthentication();
		app.UseAuthorization();

		ConnectedPlugin.ConfigureApplication(app, Configuration);
		((IPlugableComponent)AutoScavengePlugin).ConfigureApplication(app, Configuration);
	}

	class FakePublisher : IPublisher {
		public void Publish(Message message) {
			// No-op
		}
	}

	public class FakeLicenseService : ILicenseService {
		readonly BehaviorSubject<License> _licenseSubject;

		public FakeLicenseService(string token) {
			SelfLicense = new License(new(token));
			CurrentLicense = SelfLicense; // they wouldn't normally be the same
			_licenseSubject = new BehaviorSubject<License>(CurrentLicense);
		}

		public License SelfLicense { get; }

		public License? CurrentLicense { get; }

		public IObservable<License> Licenses => _licenseSubject;

		public void RejectLicense(Exception ex) {
		}

		public void EmitLicense(License x) {
			_licenseSubject.OnNext(x);
		}
	}
}
