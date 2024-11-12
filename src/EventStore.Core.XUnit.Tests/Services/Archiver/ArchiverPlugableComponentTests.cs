// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using EventStore.Core.Services.Archiver;
using EventStore.Plugins;
using EventStore.Plugins.Licensing;
using EventStore.Plugins.Transforms;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services.Archiver;

public class ArchiverPlugableComponentTests {
	[Theory]
	[InlineData(true, true, "ARCHIVE", false)]
	[InlineData(true, false, "ARCHIVE", true)]
	[InlineData(false, true, "ARCHIVE", false)]
	[InlineData(false, false, "ARCHIVE", false)]
	[InlineData(true, true, "NONE", true)]
	[InlineData(true, false, "NONE", true)]
	[InlineData(false, true, "NONE", false)]
	[InlineData(false, false, "NONE", false)]
	public void respects_license(bool enabled, bool licensePresent, string entitlement, bool expectedException) {
		// given
		var sut = new ArchiverPlugableComponent();

		IConfigurationBuilder configBuilder = new ConfigurationBuilder();

		if (!enabled)
			configBuilder = configBuilder.AddInMemoryCollection(new Dictionary<string, string> {
				{"EventStore:Archive:Enabled", "false"},
			});

		var config = configBuilder.Build();

		var builder = WebApplication.CreateBuilder();

		var licenseService = new FakeLicenseService(licensePresent, entitlement);
		builder.Services.AddSingleton<ILicenseService>(licenseService);

		((IPlugableComponent)sut).ConfigureServices(
			builder.Services,
			config);

		var app = builder.Build();

		// when
		((IPlugableComponent)sut).ConfigureApplication(app, config);

		// then
		if (expectedException) {
			Assert.NotNull(licenseService.RejectionException);
		} else {
			Assert.Null(licenseService.RejectionException);
		}
	}
}
