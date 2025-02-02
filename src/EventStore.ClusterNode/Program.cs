// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Exceptions;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Certificates;
using EventStore.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Runtime;
using EventStore.ClusterNode;
using EventStore.ClusterNode.Components;
using EventStore.ClusterNode.Services;
using EventStore.Common.DevCertificates;
using EventStore.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Services;
using Serilog.Events;
using RuntimeInformation = System.Runtime.RuntimeInformation;


var configuration = EventStoreConfiguration.Build(args);

ThreadPool.SetMaxThreads(1000, 1000);
var exitCodeSource = new TaskCompletionSource<int>();
var cts = new CancellationTokenSource();

Log.Logger = EventStoreLoggerConfiguration.ConsoleLog;
try {
	var options = ClusterVNodeOptions.FromConfiguration(configuration);

	var logsDirectory = string.IsNullOrWhiteSpace(options.Logging.Log)
		? Locations.DefaultLogDirectory
		: options.Logging.Log;

	EventStoreLoggerConfiguration.Initialize(logsDirectory, options.GetComponentName(),
		options.Logging.LogConsoleFormat,
		options.Logging.LogFileSize,
		options.Logging.LogFileInterval,
		options.Logging.LogFileRetentionCount,
		options.Logging.DisableLogFile,
		options.Logging.LogConfig);

	if (options.Application.Help) {
		await Console.Out.WriteLineAsync(ClusterVNodeOptions.HelpText);
		return 0;
	}

	if (options.Application.Version) {
		await Console.Out.WriteLineAsync(VersionInfo.Text);
		return 0;
	}

	if (options.DevMode.RemoveDevCerts) {
		Log.Information("Removing EventStoreDB dev certs.");
		CertificateManager.Instance.CleanupHttpsCertificates();
		Log.Information("Dev certs removed. Exiting.");
		return 0;
	}

	Log.Information(
		"{description,-25} {version} {edition} ({buildId}/{commitSha}, {timestamp})", "ES VERSION:",
		VersionInfo.Version, VersionInfo.Edition, VersionInfo.BuildId, VersionInfo.CommitSha, VersionInfo.Timestamp
	);

	Log.Information("{description,-25} {osArchitecture} ", "OS ARCHITECTURE:", System.Runtime.InteropServices.RuntimeInformation.OSArchitecture);
	Log.Information("{description,-25} {osFlavor} ({osVersion})", "OS:", RuntimeInformation.OsPlatform, Environment.OSVersion);
	Log.Information("{description,-25} {osRuntimeVersion} ({architecture}-bit)", "RUNTIME:", RuntimeInformation.RuntimeVersion, RuntimeInformation.RuntimeMode);
	Log.Information("{description,-25} {maxGeneration} IsServerGC: {isServerGC} Latency Mode: {latencyMode}", "GC:",
		GC.MaxGeneration == 0 ? "NON-GENERATION (PROBABLY BOEHM)" : $"{GC.MaxGeneration + 1} GENERATIONS",
		GCSettings.IsServerGC,
		GCSettings.LatencyMode);
	Log.Information("{description,-25} {logsDirectory}", "LOGS:", logsDirectory);
	Log.Information(options.DumpOptions());

	var level = options.Application.AllowUnknownOptions
		? LogEventLevel.Warning
		: LogEventLevel.Fatal;

	foreach (var (option, suggestion) in options.Unknown.Options) {
		if (string.IsNullOrEmpty(suggestion)) {
			Log.Write(level, "The option {option} is not a known option.", option);
		} else {
			Log.Write(level, "The option {option} is not a known option. Did you mean {suggestion}?", option, suggestion);
		}
	}

	if (options.UnknownOptionsDetected && !options.Application.AllowUnknownOptions) {
		Log.Fatal($"Found unknown options. To continue anyway, set {nameof(ClusterVNodeOptions.ApplicationOptions.AllowUnknownOptions)} to true.");
		Log.Information("Use the --help option in the command line to see the full list of EventStoreDB configuration options.");
		return 1;
	}

	CertificateProvider certificateProvider;
	if (options.DevMode.Dev) {
		Log.Information("Dev mode is enabled.");
		Log.Warning(
			"\n==============================================================================================================\n" +
			"DEV MODE IS ON. THIS MODE IS *NOT* RECOMMENDED FOR PRODUCTION USE.\n" +
			"DEV MODE WILL GENERATE AND TRUST DEV CERTIFICATES FOR RUNNING A SINGLE SECURE NODE ON LOCALHOST.\n" +
			"==============================================================================================================\n");
		var manager = CertificateManager.Instance;
		var result = manager.EnsureDevelopmentCertificate(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1));
		if (result is not (EnsureCertificateResult.Succeeded or EnsureCertificateResult.ValidCertificatePresent)) {
			Log.Fatal("Could not ensure dev certificate is available. Reason: {result}", result);
			return 1;
		}

		var userCerts = manager.ListCertificates(StoreName.My, StoreLocation.CurrentUser, true);
		var machineCerts = manager.ListCertificates(StoreName.My, StoreLocation.LocalMachine, true);
		var certs = userCerts.Concat(machineCerts).ToList();

		if (!certs.Any()) {
			Log.Fatal("Could not create dev certificate.");
			return 1;
		}

		if (!manager.IsTrusted(certs[0]) && RuntimeInformation.IsWindows) {
			Log.Information("Dev certificate {cert} is not trusted. Adding it to the trusted store.", certs[0]);
			manager.TrustCertificate(certs[0]);
		} else {
			Log.Warning("Automatically trusting dev certs is only supported on Windows.\n" +
			            "Please trust certificate {cert} if it's not trusted already.", certs[0]);
		}

		Log.Information("Running in dev mode using certificate '{cert}'", certs[0]);
		certificateProvider = new DevCertificateProvider(certs[0]);
	} else {
		certificateProvider = new OptionsCertificateProvider();
	}

	var deprecationWarnings = options.GetDeprecationWarnings();
	if (deprecationWarnings != null) {
		Log.Warning($"DEPRECATED{Environment.NewLine}{deprecationWarnings}");
	}

	if (!ClusterVNodeOptionsValidator.ValidateForStartup(options)) {
		return 1;
	}

	if (options.Application.Insecure) {
		Log.Warning(
			"\n==============================================================================================================\n" +
			"INSECURE MODE IS ON. THIS MODE IS *NOT* RECOMMENDED FOR PRODUCTION USE.\n" +
			"INSECURE MODE WILL DISABLE ALL AUTHENTICATION, AUTHORIZATION AND TRANSPORT SECURITY FOR ALL CLIENTS AND NODES.\n" +
			"==============================================================================================================\n");
	}

	if (options.Application.WhatIf) {
		return 0;
	}

	Application.RegisterExitAction(code => {
		// add a small delay to allow the host to start up in case there's a premature shutdown
		cts.CancelAfter(TimeSpan.FromSeconds(1));
		exitCodeSource.SetResult(code);
	});

	Console.CancelKeyPress += delegate {
		Application.Exit(0, "Cancelled.");
	};

	using (var hostedService = new ClusterVNodeHostedService(options, certificateProvider, configuration)) {
		using var signal = new ManualResetEventSlim(false);
		_ = Run(hostedService, signal);
		// ReSharper disable MethodSupportsCancellation
		signal.Wait();
		// ReSharper restore MethodSupportsCancellation
	}

	return await exitCodeSource.Task;

	async Task Run(ClusterVNodeHostedService hostedService, ManualResetEventSlim signal) {
		var monitoringService = new MonitoringService();

		try {
			var builder = WebApplication.CreateBuilder(args);
			builder.Configuration.AddEnvironmentVariables("DOTNET_");
			builder.Configuration.AddConfiguration(configuration);
			builder.Logging.ClearProviders().AddSerilog();
			builder.Services.Configure<KestrelServerOptions>(configuration.GetSection("Kestrel"));
			builder.Services.Configure<HostOptions>(x => {
				x.ShutdownTimeout = TimeSpan.FromSeconds(5);
#if DEBUG
				x.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
#else
				x.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
#endif
			});
			builder.WebHost.ConfigureKestrel(server => {
				server.Limits.Http2.KeepAlivePingDelay = TimeSpan.FromMilliseconds(options.Grpc.KeepAliveInterval);
				server.Limits.Http2.KeepAlivePingTimeout = TimeSpan.FromMilliseconds(options.Grpc.KeepAliveTimeout);

				server.Listen(options.Interface.NodeIp, options.Interface.NodePort, listenOptions => {
					ConfigureHttpOptions(listenOptions, hostedService, useHttps: !hostedService.Node.DisableHttps);
				});

				if (hostedService.Node.EnableUnixSocket)
					TryListenOnUnixSocket(hostedService, server);
			});
			hostedService.Node.Startup.ConfigureServices(builder.Services);
			builder.Services.AddSingleton<IHostedService>(hostedService);
			builder.Services.AddSingleton<Preferences>();
			builder.Services.AddDataProtection().PersistKeysToFileSystem(new(hostedService.Node.Db.Config.Path));
			builder.Services.AddRazorComponents().AddInteractiveServerComponents();
			builder.Services.AddMudServices();
			builder.Services.AddMudMarkdownServices();
			builder.Services.AddSingleton(options);
			builder.Services.AddScoped<LogObserver>();
			builder.Services.AddScoped<StatsService>();
			builder.Services.AddSingleton(monitoringService);

			var app = builder.Build();
			hostedService.Node.Startup.Configure(app);
			app.UseStaticFiles();
			app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
			await app.RunAsync(cts.Token);
			exitCodeSource.TrySetResult(0);
		} catch (Exception ex) {
			Log.Fatal(ex, "Exiting");
			exitCodeSource.TrySetResult(1);
		} finally {
			monitoringService.Dispose();
			signal.Set();
		}
	}
} catch (InvalidConfigurationException ex) {
	Log.Fatal("Invalid configuration: " + ex.Message);
	return 1;
} catch (Exception ex) {
	Log.Fatal(ex, "Host terminated unexpectedly.");
	return 1;
} finally {
	await Log.CloseAndFlushAsync();
}

static void ConfigureHttpOptions(ListenOptions listenOptions, ClusterVNodeHostedService hostedService, bool useHttps) {
	if (useHttps)
		listenOptions.UseHttps(CreateServerOptionsSelectionCallback(hostedService), null);
	else
		listenOptions.Use(next => new ClearTextHttpMultiplexingMiddleware(next).OnConnectAsync);
}

static void TryListenOnUnixSocket(ClusterVNodeHostedService hostedService, KestrelServerOptions server) {
	if (hostedService.Node.Db.Config.InMemDb) {
		Log.Information("Not listening on a UNIX domain socket since the database is running in memory.");
		return;
	}

	if (!RuntimeInformation.IsLinux && !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17063)) {
		Log.Error("Not listening on a UNIX domain socket since it is not supported by the operating system.");
		return;
	}

	try {
		var unixSocket = Path.GetFullPath(Path.Combine(hostedService.Node.Db.Config.Path, "eventstore.sock"));

		if (File.Exists(unixSocket)) {
			try {
				File.Delete(unixSocket);
				Log.Information("Cleaned up stale UNIX domain socket: {unixSocket}", unixSocket);
			} catch (Exception ex) {
				Log.Error(ex, "Failed to clean up stale UNIX domain socket: {unixSocket}. Please delete the file manually.", unixSocket);
				throw;
			}
		}

		server.ListenUnixSocket(unixSocket, listenOptions => {
			listenOptions.Use(next => new UnixSocketConnectionMiddleware(next).OnConnectAsync);
			ConfigureHttpOptions(listenOptions, hostedService, useHttps: false);
		});
		Log.Information("Listening on UNIX domain socket: {unixSocket}", unixSocket);
	} catch (Exception ex) {
		Log.Error(ex, "Failed to listen on UNIX domain socket.");
		throw;
	}
}

static ServerOptionsSelectionCallback CreateServerOptionsSelectionCallback(ClusterVNodeHostedService hostedService) {
	return (_, _, _, _) => {
		var serverOptions = new SslServerAuthenticationOptions {
			ServerCertificateContext = SslStreamCertificateContext.Create(
				hostedService.Node.CertificateSelector(),
				hostedService.Node.IntermediateCertificatesSelector(),
				offline: true),
			ClientCertificateRequired = true, // request a client certificate but it's not necessary for the client to supply one
			RemoteCertificateValidationCallback = (_, certificate, chain, sslPolicyErrors) => {
				if (certificate == null) // not necessary to have a client certificate
					return true;

				var (isValid, error) =
					hostedService.Node.InternalClientCertificateValidator(certificate, chain, sslPolicyErrors);
				if (!isValid && error != null) {
					Log.Error("Client certificate validation error: {e}", error);
				}

				return isValid;
			},
			CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
			EnabledSslProtocols = SslProtocols.None, // let the OS choose a secure TLS protocol
			ApplicationProtocols = [SslApplicationProtocol.Http2, SslApplicationProtocol.Http11],
			AllowRenegotiation = false
		};

		return ValueTask.FromResult(serverOptions);
	};
}
