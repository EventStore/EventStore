// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using EventStore.Core.Configuration.Sources;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;
using Assert = NUnit.Framework.Assert;

namespace EventStore.Core.XUnit.Tests.Configuration.Sources.Legacy;

public class JsonFileConfigurationTests {
	public const string KurrentConfigFile = "test.kurrentdb.json";
	public const string EventStoreConfigFile = "test.eventstore.json";

	private static string GetDirectoryForConfigFile(string fileName) {
		var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration", fileName);
		var directory = Path.GetDirectoryName(filePath);
		Assert.NotNull(directory);
		return directory;
	}

	[Fact]
	public void LoadsConfigFromEventStoreJsonFile() {
		// Arrange
		var directory = GetDirectoryForConfigFile(EventStoreConfigFile);

		// Act
		var config = new ConfigurationBuilder()
			.AddLegacyEventStoreJsonFile(config => {
				config.FileProvider = new PhysicalFileProvider(directory) {
					UseActivePolling = false,
					UsePollingFileWatcher = false
				};
				config.OnLoadException = _ =>
					Assert.Fail($"Could not find test config file '{EventStoreConfigFile}' in '{directory}'");
				config.Path = EventStoreConfigFile;
			}).Build();

		// Assert
		config.GetValue<bool>("Kurrent:AutoScavenge:Enabled").Should().Be(true);
		config.GetValue<bool>("Kurrent:Connectors:Enabled").Should().Be(false);
		config.GetValue<string>("Kurrent:Licensing:LicenseKey").Should().Be("invalid");
	}

	[Fact]
	public void KurrentConfigOverridesEventStoreConfig() {
		// Arrange
		var eventStoreDirectory = GetDirectoryForConfigFile(EventStoreConfigFile);
		var kurrentDirectory = GetDirectoryForConfigFile(KurrentConfigFile);

		// Act
		var config = new ConfigurationBuilder()
			.AddLegacyEventStoreJsonFile(config => {
				config.FileProvider = new PhysicalFileProvider(eventStoreDirectory) {
					UseActivePolling = false,
					UsePollingFileWatcher = false
				};
				config.OnLoadException = _ =>
					Assert.Fail($"Could not find test config file '{EventStoreConfigFile}' in '{eventStoreDirectory}'");
				config.Path = EventStoreConfigFile;
			}).AddJsonFile(config => {
				config.FileProvider = new PhysicalFileProvider(kurrentDirectory) {
					UseActivePolling = false,
					UsePollingFileWatcher = false
				};
				config.OnLoadException = _ =>
					Assert.Fail($"Could not find test config file '{KurrentConfigFile}' in '{kurrentDirectory}'");
				config.Path = KurrentConfigFile;
			}).Build();

		// Assert
		config.GetValue<bool>("Kurrent:AutoScavenge:Enabled").Should().Be(false);
		config.GetValue<string>("Kurrent:Licensing:LicenseKey").Should().Be("valid");
		config.GetValue<bool>("Kurrent:Connectors:Enabled").Should().Be(false);
	}
}
