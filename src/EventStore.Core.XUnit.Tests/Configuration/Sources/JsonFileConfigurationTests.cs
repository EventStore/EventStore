// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using EventStore.Core.Configuration.Sources;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Configuration;

public class JsonFileConfigurationTests {
	[Fact]
	public void LoadsConfigFromKurrentJsonFile() {
		// Arrange
		var fileName = "test.kurrentdb.json";
		var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration", fileName);
		var directory = Path.GetDirectoryName(filePath);
		Assert.NotNull(directory);

		// Act
		var config = new ConfigurationBuilder()
			.AddJsonFile(config => {
				config.FileProvider = new PhysicalFileProvider(directory) {
					UseActivePolling = false,
					UsePollingFileWatcher = false
				};
				config.OnLoadException = _ =>
					Assert.Fail($"Could not find test config file '{fileName}' in '{directory}'");
				config.Path = fileName;
			}).Build();

		// Assert
		config.GetValue<bool>($"{KurrentConfigurationKeys.Prefix}:AutoScavenge:Enabled").Should().Be(false);
		config.GetValue<string>($"{KurrentConfigurationKeys.Prefix}:Licensing:LicenseKey").Should().Be("valid");
	}
}
