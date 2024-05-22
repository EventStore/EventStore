#nullable enable

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Plugins;
using EventStore.Plugins.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventStore.Core.Authorization;

public abstract class AuthorizationProviderBase : Plugin, IAuthorizationProvider {
	protected AuthorizationProviderBase(
		string? name = null,
		string? version = null,
		string? licensePublicKey = null,
		string? diagnosticsName = null,
		params KeyValuePair<string, object?>[] diagnosticsTags) :
		base(name, version, licensePublicKey, diagnosticsName, diagnosticsTags) { }

	protected AuthorizationProviderBase(PluginOptions options) : 
		this(options.Name, options.Version, options.LicensePublicKey, options.DiagnosticsName, options.DiagnosticsTags) { }
	
	public abstract ValueTask<bool> CheckAccessAsync(ClaimsPrincipal principal, Operation operation, CancellationToken cancellationToken);
	
	// TODO SS: fix visibility on EventStore.Plugins, but we might not even need this method
	public new virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration) =>
		base.ConfigureServices(services, configuration);
	
	// TODO SS: fix visibility on EventStore.Plugins, but we might not even need this method
	public new virtual void ConfigureApplication(IApplicationBuilder app, IConfiguration configuration) =>
		base.ConfigureApplication(app, configuration);
}