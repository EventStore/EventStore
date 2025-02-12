// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.PluginHosting;
using EventStore.Plugins.Subsystems;
using EventStore.POC.IO.Core;
using EventStore.POC.PluginHost;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var path = "plugins";

InitializeLogging();

Console.WriteLine("Hello, connected subsystems!");

var di = new DirectoryInfo(path);
using var pluginLoader = new PluginLoader(di);

var builder = WebApplication.CreateBuilder();

//
// setup connected subsystems
//
var client = GenClient(2113);
var count = 0;
var mvc = builder.Services.AddControllers();
var subsystems = new List<ISubsystem>();
var config = new ConfigurationBuilder().Build();
foreach (var plugin in pluginLoader.Load<IConnectedSubsystemsPlugin>().ToArray()) {
	try {
		foreach (var subsystem in plugin.GetSubsystems()) {
			subsystems.Add(subsystem);
			subsystem.ConfigureServices(builder.Services, config);
			count++;
		}

		// import controllers
		mvc.AddApplicationPart(plugin.GetType().Assembly);
	} catch (Exception ex) {
		Console.WriteLine("Error loading plugin: {0}", ex); //qq
	}
}

Console.WriteLine("Loaded {0} from {1}", count, di.FullName);

builder.Services
	.AddSingleton(client)
	.AddSingleton<IClient>(new ExternalClient(client))
	.AddSingleton<IEnumerable<ISubsystem>>(subsystems)
	.AddSingleton<IHostedService, SubsystemHost>()
	.AddAuthorization()
	.AddAuthentication()
	.AddScheme<AuthenticationSchemeOptions, MyAuthenticationHandler>(
		authenticationScheme: "event store query",
		configureOptions: x => { });

//
// setup request pipeline
//
var app = builder.Build();
//qq authentication, authorization & https
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

var appBuilder = app.UseRouting();
foreach (var subsystem in subsystems) {
	subsystem.ConfigureApplication(appBuilder, config);
}

// run the web server & wait
await app.RunAsync("https://localhost:2119");


static EventStoreClient GenClient(int port) {
	var connectionString = $"esdb://admin:changeit@localhost:{port}?tls=true&tlsVerifyCert=false";
	Console.WriteLine($"Connecting to EventStoreDB at: `{connectionString}`");
	var settings = EventStoreClientSettings.Create(connectionString);
	settings.ConnectivitySettings.NodePreference = NodePreference.Random;
	return new EventStoreClient(settings);
}

static void InitializeLogging() {
	//qq reference EventStore.Common?
	const string consoleOutputTemplate =
		"[{ProcessId,5},{ThreadId,2},{Timestamp:HH:mm:ss.fff},{Level:u3}] {Message}{NewLine}{Exception}";

	Log.Logger = new LoggerConfiguration()
		//.Enrich.WithProcessId()
		//.Enrich.WithThreadId()
		.Enrich.FromLogContext()
		.WriteTo.Console(outputTemplate: consoleOutputTemplate)
		.CreateLogger();
}

public class SubsystemHost : IHostedService {
	private readonly IEnumerable<ISubsystem> _subsystems;

	public SubsystemHost(IEnumerable<ISubsystem> subsystems) {
		_subsystems = subsystems;
	}

	// IHostService
	public async Task StartAsync(CancellationToken cancellationToken) {
		foreach (var subsystem in _subsystems) {
			await subsystem.Start();
		}
	}

	public async Task StopAsync(CancellationToken cancellationToken) {
		foreach (var subsystem in _subsystems) {
			await subsystem.Stop();
		}
	}
}
