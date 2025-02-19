// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using EventStore.Plugins.Licensing;
using EventStore.Plugins.TestHelpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventStore.Auth.OAuth.Tests;


public class OAuthAuthenticationPluginTests {
	private readonly string _configFile;

	public OAuthAuthenticationPluginTests() {
		_configFile = Path.Combine(Environment.CurrentDirectory, "conf", "oauth.conf");
	}

	[Theory]
	[InlineData(true, "OAUTH_AUTHENTICATION", false)]
	[InlineData(true, "NONE", true)]
	[InlineData(false, "NONE", true)]
	public void respects_license(bool licensePresent, string entitlement, bool expectedException) {
		// given
		var sut = new OAuthAuthenticationPlugin()
			.GetAuthenticationProviderFactory(_configFile)
			.Build(false);

		var config = new ConfigurationBuilder().Build();
		var builder = WebApplication.CreateBuilder();

		var licenseService = new Fixtures.FakeLicenseService(licensePresent, entitlement);
		builder.Services.AddSingleton<ILicenseService>(licenseService);

		sut.ConfigureServices(
			builder.Services,
			config);

		var app = builder.Build();

		// when
		sut.ConfigureApplication(app, config);

		// then
		if (expectedException) {
			Assert.NotNull(licenseService.RejectionException);
		} else {
			Assert.Null(licenseService.RejectionException);
		}
	}
}
