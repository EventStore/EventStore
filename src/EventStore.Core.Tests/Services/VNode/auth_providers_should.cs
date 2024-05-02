using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Tests.Helpers;
using EventStore.Plugins.Authentication;
using EventStore.Plugins.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Serilog;

namespace EventStore.Core.Tests.Services.VNode;

[TestFixture]
public class auth_providers_should : SpecificationWithDirectory {
	[Test]
	public async Task be_registered_with_di() {
		var authenticationConfigured = new TaskCompletionSource();
		var authenticationServicesConfigured = new TaskCompletionSource();
		var authorizationConfigured = new TaskCompletionSource();
		var authorizationServicesConfigured = new TaskCompletionSource();

		await using var node = new MiniNode<LogFormat.V2, string>(
			PathName,
			authenticationProviderFactory: new FakeAuthenticationProviderFactory(authenticationConfigured, authenticationServicesConfigured),
			authorizationProviderFactory: new FakeAuthorizationProviderFactory(authorizationConfigured, authorizationServicesConfigured));

		_ = node.Start();

		await authenticationConfigured.Task.WithTimeout(TimeSpan.FromSeconds(5));
		await authenticationServicesConfigured.Task.WithTimeout(TimeSpan.FromSeconds(5));
		await authorizationConfigured.Task.WithTimeout(TimeSpan.FromSeconds(5));
		await authorizationServicesConfigured.Task.WithTimeout(TimeSpan.FromSeconds(5));
	}

	private class FakeAuthenticationProviderFactory(
		TaskCompletionSource ConfigureTcs,
		TaskCompletionSource ConfigureServicesTcs)
		: IAuthenticationProviderFactory {
		public IAuthenticationProvider Build(bool logFailedAuthenticationAttempts, ILogger logger) =>
			new FakeAuthenticationProvider(ConfigureTcs, ConfigureServicesTcs);

		private class FakeAuthenticationProvider(
			TaskCompletionSource ConfigureTcs,
			TaskCompletionSource ConfigureServicesTcs)
			: IAuthenticationProvider {

			public string Name => throw new NotImplementedException();

			public IApplicationBuilder Configure(IApplicationBuilder builder) {
				ConfigureTcs.TrySetResult();
				return builder;
			}

			public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration) {
				ConfigureServicesTcs.TrySetResult();
				return services;
			}

			public void Authenticate(AuthenticationRequest authenticationRequest) {
				throw new NotImplementedException();
			}

			public void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder) {
			}

			public IEnumerable<KeyValuePair<string, string>> GetPublicProperties() {
				throw new NotImplementedException();
			}

			public IReadOnlyList<string> GetSupportedAuthenticationSchemes() =>
				["Basic"];

			public Task Initialize() {
				throw new NotImplementedException();
			}
		}
	}

	private class FakeAuthorizationProviderFactory(
		TaskCompletionSource ConfigureTcs,
		TaskCompletionSource ConfigureServicesTcs)
		: IAuthorizationProviderFactory {

		public IAuthorizationProvider Build() =>
			new FakeAuthorizationProvider(ConfigureTcs, ConfigureServicesTcs);

		private class FakeAuthorizationProvider(
			TaskCompletionSource ConfigureTcs,
			TaskCompletionSource ConfigureServicesTcs)
			: IAuthorizationProvider {
			public IApplicationBuilder Configure(IApplicationBuilder builder) {
				ConfigureTcs.TrySetResult();
				return builder;
			}

			public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration) {
				ConfigureServicesTcs.TrySetResult();
				return services;
			}

			public ValueTask<bool> CheckAccessAsync(ClaimsPrincipal cp, Operation operation, CancellationToken ct) {
				throw new NotImplementedException();
			}
		}
	}
}
