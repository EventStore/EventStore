// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Reactive.Subjects;
using EventStore.Licensing.Keygen;
using EventStore.Plugins;
using EventStore.Plugins.Licensing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RestSharp;
using RestSharp.Authenticators.OAuth2;
using Serilog;

namespace EventStore.Licensing;

public class LicensingPlugin : Plugin {
	private static readonly ILogger Log = Serilog.Log.ForContext<LicensingPlugin>();

	private readonly ILicenseProvider? _licenseProvider;

	public LicensingPlugin() : this(null) {
	}

	public LicensingPlugin(ILicenseProvider? licenseProvider) : base() {
		_licenseProvider = licenseProvider;
	}

	public override void ConfigureApplication(IApplicationBuilder builder, IConfiguration configuration) =>
		builder.UseEndpoints(endpoints => endpoints
			.MapGet("/license", (HttpContext context, ILicenseService licenseService) => {
				var license = licenseService.CurrentLicense;
				return license is null
					? Results.NotFound()
					: Results.Json(LicenseSummary.SelectForEndpoint(license));
			})
			.RequireAuthorization());

	public override void ConfigureServices(IServiceCollection services, IConfiguration configuration) {
		// esdbPrivateKey is not truly private (obviously, here it is in the source). circumventing the license mechanism is against the license agreement.
		var esdbPrivateKey = "MIIBPAIBAAJBAMa1FchaZ4mqR2lCvIl0oEVW8tow0cWQNxVdKhoPODVqGu0KsCDBikBEC8bIWzRtzBgllplK31o3CmCQA849AzkCAwEAAQJBALFULYpNU5UBhxUi34pzsAvxWmzpoGsFFoNUTxxOdMUExvprTltFKQ/hDAyNsc8oUg0AdBzt/jDzTce/W0WerHkCIQDq6SIeuUjWqGOG/+thcLJSj0jnNJ7NFJTPZDqaiocQDwIhANiL53qkAlWIg0uPlxARDtGI/bx5irIBn9Hed81WrnA3AiEApL4U4KkebPQwwHdwEqjfVkkIXqUnjTmW1w86jjECYX8CIQCa/Hcupdgt08j0+c6K50qN2diRXwRPpy32DZ39T38GPQIgSBL+EU/YRy6nwsqLLB+6+qtMd0s1T5kpI3l9VyNM3Uc=";
		var esdbPublicKey = "MEgCQQDGtRXIWmeJqkdpQryJdKBFVvLaMNHFkDcVXSoaDzg1ahrtCrAgwYpARAvGyFs0bcwYJZaZSt9aNwpgkAPOPQM5AgMBAAE=";

		var baseUrl = $"https://licensing.eventstore.com/v1/";
		var clientOptions = configuration.GetSection("EventStore:Licensing").Get<KeygenClientOptions>() ?? new();
		if (clientOptions.BaseUrl is not null) {
			baseUrl = clientOptions.BaseUrl;
			Log.Information("Using custom licensing URL: {Url}. This requires permission from EventStore Ltd.", baseUrl);
		}

		IObservable<LicenseInfo> licenses;

		if (string.IsNullOrWhiteSpace(clientOptions.LicenseKey)) {
			var sub = new Subject<LicenseInfo>();
			sub.OnError(new Exception("No license key specified"));
			licenses = sub;
		} else {
			var lifecycleService = new KeygenLifecycleService(
				new KeygenClient(
					clientOptions,
					restClient: new RestClient(
						new RestClientOptions(baseUrl) {
							Authenticator = new OAuth2AuthorizationRequestHeaderAuthenticator(clientOptions.LicenseKey, "License"),
							// todo: Interceptors = [new KeygenSignatureInterceptor()],
						})),
				new Fingerprint(),
				revalidationDelay: TimeSpan.FromSeconds(10));

			licenses = lifecycleService.Licenses;
			services.AddHostedService(sp => lifecycleService);
		}

		services
			// other components such as plugins can use this to subscribe to the licenses, inspect them, and reject them
			.AddSingleton<ILicenseService>(sp => {
				var licenseProvider =
					_licenseProvider ??
					new KeygenLicenseProvider(
						esdbPublicKey,
						esdbPrivateKey,
						licenses);

				return new LicenseService(
					esdbPublicKey,
					esdbPrivateKey,
					sp.GetRequiredService<IHostApplicationLifetime>(),
					licenseProvider);
			})
			.AddHostedService(sp => new LicenseTelemetryService(
				sp.GetRequiredService<ILicenseService>(),
				telemetry => PublishDiagnosticsData(telemetry, Plugins.Diagnostics.PluginDiagnosticsDataCollectionMode.Snapshot)));
	}
}
