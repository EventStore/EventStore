// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using System;
using System.Collections.Generic;
using EventStore.Common.Options;
using EventStore.Core.Configuration;
using EventStore.Core.Configuration.Sources;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Configuration;

public class ClusterVNodeOptionsPrinterTests {
	[Fact]
	public void prints_options() {
		var prefix = KurrentConfigurationKeys.Prefix.ToUpper();
		var fallbackPrefix = KurrentConfigurationKeys.FallbackPrefix.ToUpper();
		var config = new ConfigurationBuilder()
			.AddKurrentDefaultValues(new Dictionary<string, string?> {
				{ "ChunkSize", "10000"},
				{ "ChunksCacheSize", "20000" },
				{ "ClusterSize", "1" },
				{ "UnsafeAllowSurplusNodes", "false" },
			})
			.AddFallbackEnvironmentVariables(
				($"{fallbackPrefix}_WORKER_THREADS", "5"),
				($"{fallbackPrefix}_NODE_PRIORITY", "6"),
				($"{fallbackPrefix}_ANOTHER_UNKNOWN_OPTIONS", "7"))
			.AddKurrentEnvironmentVariables(
				($"{prefix}_SOME_UNKNOWN_OPTION", "77"),
				($"{prefix}_CLUSTER_GOSSIP_PORT", "99"),
				($"{prefix}_UNSAFE_ALLOW_SURPLUS_NODES", "true"),
				($"{prefix}_CONFIG", "/path/to/config/envvar"),
				($"{prefix}_RUN_PROJECTIONS", "All"))
			.AddKurrentCommandLine(
				"--config", "/path/to/config/commandline",
				"--cluster-gossip-port=88",
				$"--default-ops-password={Guid.NewGuid()}")
			.Build();

		var loadedOptions = ClusterVNodeOptions.GetLoadedOptions(config);

		var output = ClusterVNodeOptionsPrinter.Print(loadedOptions);

		output.Should().Be(@"
MODIFIED OPTIONS:
    APPLICATION OPTIONS:
         CONFIG:                          /path/to/config/commandline (Command Line)
         WORKER THREADS:                  5 (Fallback Environment Variables)

    CLUSTER OPTIONS:
         CLUSTER GOSSIP PORT:             88 (Command Line)
         NODE PRIORITY:                   6 (Fallback Environment Variables)
         UNSAFE ALLOW SURPLUS NODES:      true (Environment Variables)

    DEFAULT USER OPTIONS:
         DEFAULT OPS PASSWORD:            ******** (Command Line)

    PROJECTION OPTIONS:
         RUN PROJECTIONS:                 All (Environment Variables)


DEFAULT OPTIONS:
    CLUSTER OPTIONS:
         CLUSTER SIZE:                    1 (<DEFAULT>)

    DATABASE OPTIONS:
         CHUNK SIZE:                      10000 (<DEFAULT>)
         CHUNKS CACHE SIZE:               20000 (<DEFAULT>)
");
	}

	[Fact]
	public void loaded_options_do_not_contain_sensitive_values() {
		var secretText = Guid.NewGuid().ToString();

		var config = new ConfigurationBuilder()
			.AddKurrentDefaultValues()
			.AddFallbackEnvironmentVariables(
				($"{KurrentConfigurationKeys.FallbackPrefix.ToUpper()}_DEFAULT_ADMIN_PASSWORD", secretText))
			.AddFallbackCommandLine(
				$"--{KurrentConfigurationKeys.FallbackPrefix}:CertificatePassword", secretText)
			.AddKurrentCommandLine($"--default-ops-password={secretText}")
			.Build();

		var loadedOptions = ClusterVNodeOptions.GetLoadedOptions(config);

		var opsPassword = loadedOptions[$"{KurrentConfigurationKeys.Prefix}:DefaultOpsPassword"];
		var adminPassword = loadedOptions[$"{KurrentConfigurationKeys.Prefix}:DefaultAdminPassword"];
		var certPassword = loadedOptions[$"{KurrentConfigurationKeys.Prefix}:CertificatePassword"];

		opsPassword.DisplayValue.Should().BeEquivalentTo("********");
		adminPassword.DisplayValue.Should().BeEquivalentTo("********");
		certPassword.DisplayValue.Should().BeEquivalentTo("********");
	}

	[Fact]
	public void loaded_options_show_allowed_values() {
		var config = new ConfigurationBuilder()
			.AddKurrentDefaultValues()
			.Build();

		var loadedOptions = ClusterVNodeOptions.GetLoadedOptions(config);

		var option = loadedOptions[$"{KurrentConfigurationKeys.Prefix}:DbLogFormat"];

		option.Metadata.AllowedValues.Should().BeEquivalentTo(Enum.GetNames(typeof(DbLogFormat)));
	}

	[Fact]
	public void loaded_option_provided_by_another_source_shows_the_correct_source() {
		var prefix = KurrentConfigurationKeys.Prefix;
		var fallbackPrefix = KurrentConfigurationKeys.FallbackPrefix;
		var config = new ConfigurationBuilder()
			.AddKurrentDefaultValues()
			.AddFallbackEnvironmentVariables(
				($"{fallbackPrefix.ToUpper()}_CLUSTER_SIZE", "10"),
				($"{fallbackPrefix.ToUpper()}_NODE_PRIORITY", "11"))
			.AddKurrentEnvironmentVariables(
				($"{prefix.ToUpper()}_CLUSTER_SIZE", "15"),
				($"{prefix.ToUpper()}_LOG_LEVEL", "Fatal"),
				($"{prefix.ToUpper()}_LOG_FORMAT", "json"))
			.AddFallbackCommandLine(
				$"--{fallbackPrefix}:NodePort=17",
				$"--{fallbackPrefix}:ReadOnlyReplica=true")
			.AddKurrentCommandLine(
				$"--log-level=Information",
				$"--{prefix}:NodePort=18")
			.Build();

		var loadedOptions = ClusterVNodeOptions.GetLoadedOptions(config);

		// default
		var insecure = loadedOptions[$"{prefix}:Insecure"];
		insecure.DisplayValue.Should().BeEquivalentTo("false");
		insecure.SourceDisplayName.Should().BeEquivalentTo("<DEFAULT>");

		// fallback environment variables
		var nodePriority = loadedOptions[$"{prefix}:NodePriority"];
		nodePriority.DisplayValue.Should().BeEquivalentTo("11");
		nodePriority.SourceDisplayName.Should().BeEquivalentTo("Fallback Environment Variables");

		// environment variables
		var clusterSize = loadedOptions[$"{prefix}:ClusterSize"];
		clusterSize.DisplayValue.Should().BeEquivalentTo("15");
		clusterSize.SourceDisplayName.Should().BeEquivalentTo("Environment Variables");

		// fallback command line
		var readOnly = loadedOptions[$"{prefix}:ReadOnlyReplica"];
		readOnly.DisplayValue.Should().BeEquivalentTo("true");
		readOnly.SourceDisplayName.Should().BeEquivalentTo("Fallback Command Line");

		// command line
		var logLevel = loadedOptions[$"{prefix}:LogLevel"];
		logLevel.DisplayValue.Should().BeEquivalentTo("Information");
		logLevel.SourceDisplayName.Should().BeEquivalentTo("Command Line");

		// command line - override fallback
		var nodePort = loadedOptions[$"{prefix}:NodePort"];
		nodePort.DisplayValue.Should().BeEquivalentTo("18");
		nodePort.SourceDisplayName.Should().BeEquivalentTo("Command Line");
	}
}
