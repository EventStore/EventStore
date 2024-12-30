// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using System;
using System.Collections;
using EventStore.Common.Configuration;
using EventStore.Core.Configuration.Sources;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration;

public static class KurrentConfiguration {
	public static IConfigurationRoot Build(string[] args, IDictionary environment) {

		// resolve the main configuration file
		var configFile = ResolveConfigurationFile(args, environment);

		// Create a single IConfiguration object that contains the whole configuration, including
		// plugin configuration. We will add it to the DI and make it available to the plugins.
		//
		// Three json files are loaded explicitly for backwards compatibility
		// - metricsconfig.json needs loading into the EventStore:Metrics section.
		// - kestrelsettings.json is not located in a config/ directory
		// - logconfig.json is not located in a config/ directory
		//
		// The EventStore configuration section is kept for backwards compatibility
		var builder = new ConfigurationBuilder()
			// we should be able to stop doing this soon as long as we bind the options automatically
			.AddKurrentDefaultValues()
			.AddKurrentYamlConfigFile(configFile.Path, configFile.Optional)

			.AddSection($"{KurrentConfigurationKeys.Prefix}:Metrics",
				x => x.AddKurrentConfigFile("metricsconfig.json", true, true))

			// The other config files are added to the root, and must put themselves in the appropriate sections
			.AddKurrentConfigFile("kestrelsettings.json", true, true)
			.AddKurrentConfigFile("logconfig.json", true, true)

			// Load all json files in the  `config` subdirectory (if it exists) of each configuration
			// directory. We use the subdirectory to ensure that we only load configuration files.
			.AddFallbackConfigFiles("*.json")
			.AddKurrentConfigFiles("*.json")

			#if DEBUG
			// load all json files in the current directory
			.AddJsonFile("appsettings.json", true)
			.AddJsonFile("appsettings.Development.json", true)
			#endif

			.AddEnvironmentVariables()
			.AddFallbackEnvironmentVariables(environment)
			.AddKurrentEnvironmentVariables(environment)

			// follows current behaviour yet Env Vars should take precedence
			// to handle real-world deployment pipeline scenarios
			.AddFallbackCommandLine(args)
			.AddKurrentCommandLine(args);

		return builder.Build();
	}

	public static IConfigurationRoot Build(params string[] args) {
		var environment = Environment.GetEnvironmentVariables();
		return Build(args, environment);
	}

	private static (string Path, bool Optional) ResolveConfigurationFile(string[] args, IDictionary environment) {
		var configuration = new ConfigurationBuilder()
			.AddKurrentEnvironmentVariables(environment)
			.AddKurrentCommandLine(args)
			.Build();

		var configFilePath = configuration.GetValue<string?>($"{KurrentConfigurationKeys.Prefix}:Config");

		return string.IsNullOrEmpty(configFilePath)
			// get the default config file path
			? (new ClusterVNodeOptions.ApplicationOptions().Config, true)
			// if the user has specified a config file make it non optional
			: (configFilePath, false);
	}

	public static IConfigurationBuilder AddKurrentYamlConfigFile(
		this IConfigurationBuilder builder,
		string path,
		bool optional) =>

		builder.AddSection(
			KurrentConfigurationKeys.Prefix,
			x => x.AddYamlFile(
				path: path,
				optional: optional,
				reloadOnChange: true));
}
