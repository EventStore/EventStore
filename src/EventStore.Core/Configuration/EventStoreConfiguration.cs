#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EventStore.Common.Configuration;
using EventStore.Common.Utils;
using EventStore.Core.Configuration.Sources;
using EventStore.Core.Util;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration {
	public static class EventStoreConfiguration {
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
			var builder = new ConfigurationBuilder()
				// we should be able to stop doing this soon as long as we bind the options automatically
				.AddEventStoreDefaultValues()
				.AddEventStoreYamlConfigFile(configFile.Path, configFile.Optional)

				.AddSection("EventStore:Metrics", x => x.AddEsdbConfigFile("metricsconfig.json", true, true))

				// The other config files are added to the root, and must put themselves in the appropriate sections
				.AddEsdbConfigFile("kestrelsettings.json", true, true)
				.AddEsdbConfigFile("logconfig.json", true, true)

				// Load all json files in the  `config` subdirectory (if it exists) of each configuration
				// directory. We use the subdirectory to ensure that we only load configuration files.
				.AddEsdbConfigFiles("*.json")

				.AddEnvironmentVariables()
				.AddEventStoreEnvironmentVariables(environment)

				// follows current behaviour yet Env Vars should take precedence
				// to handle real-world deployment pipeline scenarios
				.AddEventStoreCommandLine(args);

			return builder.Build();
		}

		public static IConfigurationRoot Build(params string[] args) {
			var environment = Environment.GetEnvironmentVariables();
			return Build(args, environment);
		}

		private static (string Path, bool Optional) ResolveConfigurationFile(string[] args, IDictionary environment) {
			var configuration = new ConfigurationBuilder()
				.AddEventStoreEnvironmentVariables(environment)
				.AddEventStoreCommandLine(args)
				.Build();

			var configFilePath = configuration.GetValue<string?>("EventStore:Config");

			return string.IsNullOrEmpty(configFilePath)
				// get the default config file path
				? (new ClusterVNodeOptions.ApplicationOptions().Config, true)
				// if the user has specified a config file make it non optional
				: (configFilePath, false);
		}

		public static IConfigurationBuilder AddEventStoreYamlConfigFile(this IConfigurationBuilder builder, string path,
			bool optional = true) {
			if (string.IsNullOrWhiteSpace(path))
				throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));

			var yamlSource = new YamlConfigurationSource {
				Path = path,
				Optional = optional,
				ReloadOnChange = true,
				Prefix = EventStoreConfigurationKeys.Prefix
			};

			yamlSource.ResolveFileProvider();

			return builder.Add(yamlSource);
		}
	}
}
