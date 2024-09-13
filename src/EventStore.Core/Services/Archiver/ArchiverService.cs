using System.Collections.Generic;
using EventStore.Plugins;
using EventStore.Plugins.Licensing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Archiver;

public class ArchiverService : IPlugableComponent {
	protected static readonly ILogger Log = Serilog.Log.ForContext<ArchiverService>();

	public string Name => "Archiver";

	public string DiagnosticsName => Name;

	public KeyValuePair<string, object>[] DiagnosticsTags => [];

	public string Version => "0.0.1";

	public bool Enabled => true;

	public string LicensePublicKey => LicenseConstants.LicensePublicKey;

	public void ConfigureApplication(IApplicationBuilder builder, IConfiguration configuration) {
		_ = LicenseMonitor.MonitorAsync(
			featureName: Name,
			requiredEntitlements: [],
			licenseService: builder.ApplicationServices.GetRequiredService<ILicenseService>(),
			logger: builder.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType()));
	}

	public void ConfigureServices(IServiceCollection services, IConfiguration configuration) {
	}
}
