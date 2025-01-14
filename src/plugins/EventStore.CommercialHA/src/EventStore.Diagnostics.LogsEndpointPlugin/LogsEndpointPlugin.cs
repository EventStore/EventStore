// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using EventStore.Plugins;
using EventStore.Plugins.Diagnostics;
using EventStore.Plugins.Licensing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Serilog.ILogger;

namespace EventStore.Diagnostics.LogsEndpointPlugin;

// TODO: use SubsystemPlugin now that it is more flexible about licence requirements and what to do when they fail
public class LogsEndpointPlugin() : SubsystemsPlugin(
	name: "LogsEndpoint",
	version: typeof(LogsEndpointPlugin).Assembly.GetName().Version!.ToString()){

	private static readonly ILogger _logger = Log.ForContext<LogsEndpointPlugin>();
	private const string EndpointPath = "/admin/logs";
	private EventStoreOptions? _options;

	public override (bool Enabled, string EnableInstructions) IsEnabled(IConfiguration configuration) {
		return (true, "");
	}

	void DisableEndpoint(Exception ex) {
		var msg = "LogsEndpoint is not licensed, stopping.";
		Log.Information(msg);
		Disable(msg);
		PublishDiagnosticsData(
			new Dictionary<string, object?>() { ["enabled"] = Enabled });
		Stop();
	}

	public override void ConfigureApplication(IApplicationBuilder builder, IConfiguration configuration) {
		_options = configuration.GetSection("EventStore").Get<EventStoreOptions>();
		var diagnosticListener = new DiagnosticListener(DiagnosticsName);
		var value = new PluginDiagnosticsData {
			Source = DiagnosticsName,
			Data = new() { ["enabled"] = Enabled },
			CollectionMode = PluginDiagnosticsDataCollectionMode.Partial
		};
		diagnosticListener.Write(nameof(PluginDiagnosticsData), value);

		var licenseService = builder.ApplicationServices.GetRequiredService<ILicenseService>();
		var logger = builder.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());

		_ = LicenseMonitor.MonitorAsync(
			featureName: Name,
			requiredEntitlements: ["LOGS_ENDPOINT"],
			licenseService: licenseService,
			onLicenseException: DisableEndpoint,
			logger: logger,
			licensePublicKey: LicensePublicKey);

		if (_options == null) {
			_logger.Error("LogsEndpoint: Failed to get configuration, endpoint will not be available!");
			return;
		}

		if (_options.Log == string.Empty) {
			_logger.Error("LogsEndpoint: Failed to get the configured directory for log files, endpoint will not be available!");
			return;
		}

		if (_options.NodeIp == string.Empty) {
			_logger.Error("LogsEndpoint: Failed to get the configured node IP, endpoint will not be available!");
			return;
		}

		if (_options.NodePort == string.Empty) {
			_logger.Error("LogsEndpoint: Failed to get the configured node port, endpoint will not be available!");
			return;
		}

		var logsDir = Path.Combine(_options.Log, $"{_options.NodeIp}-{_options.NodePort}-cluster-node");
		logsDir = Path.GetFullPath(logsDir);
		if (!Directory.Exists(logsDir)) {
			_logger.Error("LogsEndpoint: Failed to find the log files directory at {LogsDir}, endpoint will not be available", logsDir);
			return;
		}

		ConfigureEndpoint(builder, logsDir);
	}

	private void ConfigureEndpoint(IApplicationBuilder builder, string logsDir) {
		_logger.Information("LogsEndpoint: Serving logs from {Dir} at endpoint {Endpoint}", logsDir, EndpointPath);

		builder.Map(EndpointPath, b => b
			.Use(async (context, next) => {
				if (!Enabled) {
					context.Response.StatusCode = (int)HttpStatusCode.NotFound;
				} else if (context.User.IsInRole("$ops") || context.User.IsInRole("$admins")) {
					await next(context);
				} else {
					context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
				}
			})
			.UseFileServer(new FileServerOptions {
				EnableDirectoryBrowsing = true,
				FileProvider = new PhysicalFileProvider(logsDir),
				DirectoryBrowserOptions = {
					Formatter = new JsonDirectoryFormatter(),
					RedirectToAppendTrailingSlash = false,
				},
				StaticFileOptions = {
					ContentTypeProvider = JsonOnlyContentTypeProvider(),
				}
			}));

		static FileExtensionContentTypeProvider JsonOnlyContentTypeProvider() {
			var c = new FileExtensionContentTypeProvider();
			c.Mappings.Clear();
			c.Mappings.Add(".json", "application/json");
			return c;
		}
	}

	private class EventStoreOptions {
		public string Log { get; init; } = string.Empty;
		public string NodeIp { get; init; } = string.Empty;
		public string NodePort { get; init; } = string.Empty;
	}
}
