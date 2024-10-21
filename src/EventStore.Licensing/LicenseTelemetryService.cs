// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Plugins.Licensing;
using Microsoft.Extensions.Hosting;

namespace EventStore.Licensing;

public class LicenseTelemetryService : IHostedService {
	private readonly ILicenseService _licenseProvider;
	private readonly Action<Dictionary<string, object?>> _publish;

	public LicenseTelemetryService(
		ILicenseService licenseProvider,
		Action<Dictionary<string, object?>> publish) {

		_licenseProvider = licenseProvider;
		_publish = publish;
	}

	public Task StartAsync(CancellationToken cancellationToken) {
		_licenseProvider.Licenses.Subscribe(
			onNext: license => {
				var telemetry = LicenseSummary.SelectForTelemetry(license);
				_publish(telemetry);
			},
			onError: ex => {
				_publish([]);
			});

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}