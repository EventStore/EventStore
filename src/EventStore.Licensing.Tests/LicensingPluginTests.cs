// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using EventStore.Plugins;
using EventStore.Plugins.Licensing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventStore.Licensing.Tests;

public class LicensingPluginTests : IAsyncLifetime {
	private TestServer? _server;

	public Task InitializeAsync() => Task.CompletedTask;

	private static LicensingPlugin CreateUnLicensedSutAsync() {
		return new LicensingPlugin(new AdHocLicenseProvider(new Exception("license is expired, say")));
	}

	private static async Task<LicensingPlugin> CreateLicensedSutAsync(Dictionary<string, object> claims) {
		using var rsa = RSA.Create(512);
		var publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());
		var privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
		var license = await License.CreateAsync(publicKey, privateKey, claims);
		return new LicensingPlugin(new AdHocLicenseProvider(license));
	}

	private static async Task<TestServer> InitializeServerAsync(LicensingPlugin sut) {
		var server = new TestServer(sut);
		await server.InitializeAsync();
		return server;
	}

	public async Task DisposeAsync() {
		if (_server != null) {
			await _server.DisposeAsync();
		}
	}

	[Fact]
	public void has_parameterless_constructor() {
		// needed for all plugins
		Activator.CreateInstance<LicensingPlugin>();
	}

	[Fact]
	public async Task given_licensed_then_endpoint_exposes_the_correct_claims() {
		// given
		var claims = new Dictionary<string, object> {
			{"another_claim", "another_value" } // excluded from endpoint
		};

		new LicenseSummary("license123", "the_company", true, true, true, true, 4, new DateTime(2024, 4, 15), "somenotes")
			.Export(claims);

		var sut = await CreateLicensedSutAsync(claims);
		_server = await InitializeServerAsync(sut);

		// when
		var result = await _server.AuthenticatedClient.GetAsync("/license");

		// then
		Assert.Equal(HttpStatusCode.OK, result.StatusCode);

		var responseString = await result.Content.ReadAsStringAsync();
		Assert.DoesNotContain("another", responseString);
		Assert.Equal("""
			{
			  "licenseId": "license123",
			  "company": "the_company",
			  "isTrial": "true",
			  "isExpired": "true",
			  "isValid": "true",
			  "isFloating": "true",
			  "daysRemaining": "4",
			  "startDate": "1713139200",
			  "notes": "somenotes"
			}
			""".Replace(" ", "").Replace(Environment.NewLine, ""),
			responseString);
	}

	[Fact]
	public async Task given_not_licensed_then_endpoint_returns_404() {
		// given
		var sut = CreateUnLicensedSutAsync();
		_server = await InitializeServerAsync(sut);

		// when
		var result = await _server.AuthenticatedClient.GetAsync("/license");

		// then
		Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
		var responseString = await result.Content.ReadAsStringAsync();
		Assert.Equal("\"license is expired, say\"", responseString);
	}

	[Fact]
	public async Task requires_authentication() {
		// given
		var sut = await CreateLicensedSutAsync(new Dictionary<string, object> {
			{"isTrial", "true" }
		});
		_server = await InitializeServerAsync(sut);

		// when
		var result = await _server.UnauthenticatedClient.GetAsync("/license");

		// then
		Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
		var responseString = await result.Content.ReadAsStringAsync();
		Assert.Equal("", responseString);
	}
}

public class TestServer(LicensingPlugin sut) : IAsyncLifetime {
	private WebApplication? _app;
	private HttpClient? _authenticatedClient;
	private HttpClient? _unauthenticatedClient;

	public HttpClient AuthenticatedClient => _authenticatedClient!;
	public HttpClient UnauthenticatedClient => _unauthenticatedClient!;

	public async Task InitializeAsync() {
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseUrls("http://127.0.0.1:0");
		builder.Services.AddAuthorization();
		builder.Services.AddAuthentication(o => {
			o.AddScheme<FakeAuthenticationHandler>("fake", "fake");
		});

		((IPlugableComponent)sut).ConfigureServices(builder.Services, builder.Configuration);
		_app = builder.Build();
		_app.UseRouting();
		_app.UseAuthorization();

		((IPlugableComponent)sut).ConfigureApplication(_app, builder.Configuration);

		await _app.StartAsync();

		// create the clients
		_authenticatedClient = new HttpClient() {
			BaseAddress = new Uri(_app.Urls.First())
		};
		_authenticatedClient.BaseAddress = new Uri(_app!.Urls.First());
		_authenticatedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
			"Basic",
			Convert.ToBase64String(Encoding.ASCII.GetBytes(
				$"{FakeAuthenticationHandler.Username}:{FakeAuthenticationHandler.Password}")));

		_unauthenticatedClient = new HttpClient() {
			BaseAddress = new Uri(_app.Urls.First())
		};
		_unauthenticatedClient.BaseAddress = new Uri(_app!.Urls.First());
	}

	public async Task DisposeAsync() {
		if (_app != null) {
			await _app.DisposeAsync();
		}
	}
}
