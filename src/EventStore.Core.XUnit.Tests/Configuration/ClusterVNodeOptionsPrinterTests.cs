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
		var config = new ConfigurationBuilder()
			.AddKurrentDefaultValues(new Dictionary<string, string?> {
				{ "ChunkSize", "10000"},
				{ "ChunksCacheSize", "20000" },
				{ "ClusterSize", "1" },
				{ "UnsafeAllowSurplusNodes", "false" },
			})
			.AddKurrentEnvironmentVariables(
				("EVENTSTORE_SOME_UNKNOWN_OPTION", "77"),
				("EVENTSTORE_CLUSTER_GOSSIP_PORT", "99"),
				("EVENTSTORE_UNSAFE_ALLOW_SURPLUS_NODES", "true"),
				("EVENTSTORE_CONFIG", "/path/to/config/envvar"),
				("EVENTSTORE_RUN_PROJECTIONS", "All"))
			.AddEventStoreCommandLine(
				"--config", "/path/to/config/commandline",
				"--cluster-gossip-port=88",
				$"--default-ops-password={Guid.NewGuid()}")
			.Build();

		var loadedOptions = ClusterVNodeOptions.GetLoadedOptions(config);

		var output = ClusterVNodeOptionsPrinter.Print(loadedOptions);

		output.Should().Be(@"
MODIFIED OPTIONS:
    APPLICATION OPTIONS:
         CONFIG:                             /path/to/config/commandline (Command Line)

    CLUSTER OPTIONS:
         CLUSTER GOSSIP PORT:                88 (Command Line)
         UNSAFE ALLOW SURPLUS NODES:         true (Environment Variables)

    DEFAULT USER OPTIONS:
         DEFAULT OPS PASSWORD:               ******** (Command Line)

    PROJECTION OPTIONS:
         RUN PROJECTIONS:                    All (Environment Variables)


DEFAULT OPTIONS:
    CLUSTER OPTIONS:
         CLUSTER SIZE:                       1 (<DEFAULT>)

    DATABASE OPTIONS:
         CHUNK SIZE:                         10000 (<DEFAULT>)
         CHUNKS CACHE SIZE:                  20000 (<DEFAULT>)
");
	}

	[Fact]
	public void loaded_options_do_not_contain_sensitive_values() {
		var secretText = Guid.NewGuid().ToString();

		var config = new ConfigurationBuilder()
			.AddKurrentDefaultValues()
			.AddEventStoreCommandLine($"--default-ops-password={secretText}")
			.Build();

		var loadedOptions = ClusterVNodeOptions.GetLoadedOptions(config);

		var option = loadedOptions["EventStore:DefaultOpsPassword"];

		option.DisplayValue.Should().BeEquivalentTo("********");
	}

	[Fact]
	public void loaded_options_show_allowed_values() {
		var config = new ConfigurationBuilder()
			.AddKurrentDefaultValues()
			.Build();

		var loadedOptions = ClusterVNodeOptions.GetLoadedOptions(config);

		var option = loadedOptions["EventStore:DbLogFormat"];

		option.Metadata.AllowedValues.Should().BeEquivalentTo(Enum.GetNames(typeof(DbLogFormat)));
	}

	[Fact]
	public void loaded_option_provided_by_another_source_shows_the_correct_source() {
		var config = new ConfigurationBuilder()
			.AddKurrentDefaultValues()
			.AddKurrentEnvironmentVariables(
				("EVENTSTORE_CLUSTER_SIZE", "15"),
				("EVENTSTORE_LOG_LEVEL", "Fatal"))
			.AddEventStoreCommandLine($"--log-level=Information")
			.Build();

		var loadedOptions = ClusterVNodeOptions.GetLoadedOptions(config);

		// default
		var insecure = loadedOptions["EventStore:Insecure"];
		insecure.DisplayValue.Should().BeEquivalentTo("false");
		insecure.SourceDisplayName.Should().BeEquivalentTo("<DEFAULT>");

		// environment variables
		var clusterSize = loadedOptions["EventStore:ClusterSize"];
		clusterSize.DisplayValue.Should().BeEquivalentTo("15");
		clusterSize.SourceDisplayName.Should().BeEquivalentTo("Environment Variables");

		// command line
		var logLevel = loadedOptions["EventStore:LogLevel"];
		logLevel.DisplayValue.Should().BeEquivalentTo("Information");
		logLevel.SourceDisplayName.Should().BeEquivalentTo("Command Line");
	}
}
