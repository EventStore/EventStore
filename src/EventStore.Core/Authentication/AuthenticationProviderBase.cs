#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.Plugins;
using EventStore.Plugins.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventStore.Core.Authentication;

public abstract class AuthenticationProviderBase : Plugin, IAuthenticationProvider {
	protected AuthenticationProviderBase(
		string? name = null,
		string? version = null,
		string? licensePublicKey = null,
		string? diagnosticsName = null,
		params KeyValuePair<string, object?>[] diagnosticsTags) :
		base(name, version, licensePublicKey, diagnosticsName, diagnosticsTags) { }

	protected AuthenticationProviderBase(PluginOptions options) :
		this(options.Name, options.Version, options.LicensePublicKey, options.DiagnosticsName, options.DiagnosticsTags) { }

	public virtual Task Initialize() => Task.CompletedTask;

	public abstract void Authenticate(AuthenticationRequest authenticationRequest);

	public virtual IEnumerable<KeyValuePair<string, string>> GetPublicProperties() => [];
	
	public virtual void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder) { }

	public virtual IReadOnlyList<string> GetSupportedAuthenticationSchemes() => [];
	
	// TODO SS: fix visibility on EventStore.Plugins, but we might not even need this method
	public new virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration) =>
		base.ConfigureServices(services, configuration);
	
	// TODO SS: fix visibility on EventStore.Plugins, but we might not even need this method
	public new virtual void ConfigureApplication(IApplicationBuilder app, IConfiguration configuration) =>
		base.ConfigureApplication(app, configuration);
}