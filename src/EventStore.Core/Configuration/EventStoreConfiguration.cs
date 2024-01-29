#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EventStore.Common.Configuration;
using EventStore.Common.Configuration.Sources;
using EventStore.Common.Utils;
using EventStore.Core.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace EventStore.Core.Configuration;

public static class EventStoreConfiguration {
	public static IConfigurationRoot Build(string[] args, IDictionary environment, KeyValuePair<string, string?>[] defaultValues) {
		// identify the environment
		var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Development;
		
		// TODO SS: should the env affect the Cluster Dev mode by default?
		
		// resolve the main configuration file
		var configFile = ResolveConfigurationFile(args, environment);
		
		string[] configDirs = [
			Path.Combine(Locations.ApplicationDirectory, "config"),
			Path.Combine(Locations.DefaultConfigurationDirectory, "config") // this is the default config dir and should take precedence 
		];
		
		// load configuration
		var builder = new ConfigurationBuilder()
			// we should be able to stop doing this soon as long as we bind the options automatically
			.AddEventStoreDefaultValues(defaultValues)
			
			.AddEventStoreConfigFile(configFile.Path, configFile.Optional)
			
			// these 3 json files are loaded explicitly for backwards compatibility
			// - metricsconfig.json needs loading into the EventStore:Metrics section.
			// - kestrelsettings.json is not located in a config/ directory
			// - logconfig.json is not located in a config/ directory
			.AddSection("EventStore:Metrics", x => x.AddConfigFile("metricsconfig.json", true, true))
			.AddConfigFile("kestrelsettings.json", true, true)
			.AddConfigFile("logconfig.json", true, true)

			// From this point onwards, do we expect these files to just follow
			// normal .net core configuration conventions?			

			.AddConfigFiles(configDirs, pattern: "*.json")
			.AddConfigFiles(configDirs, pattern: $"*.{env.ToLowerInvariant()}.json")

			.AddEnvironmentVariables()
			.AddEventStoreEnvironmentVariables(environment) 
			
			// follows current behaviour yet Env Vars should take precedence
			// to handle real-world deployment pipeline scenarios
			.AddEventStoreCommandLine(args);
		
		return builder.Build();
	}

	public static IConfigurationRoot Build(params string[] args) {
		var environment = Environment.GetEnvironmentVariables();
		
		var defaultValues = ClusterVNodeOptions.DefaultValues
			.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value?.ToString()))
			.ToArray();

		return Build(args, environment, defaultValues);
	}
	
	public static (string Path, bool Optional) ResolveConfigurationFile(string[] args, IDictionary environment) {
		var configuration = new ConfigurationBuilder()
			.AddEventStoreEnvironmentVariables(environment)
			.AddEventStoreCommandLine(args)
			.Build();
		
		var configFilePath = configuration.GetValue<string?>("EventStore:Config");

		// still dont like how we get this path
		return string.IsNullOrEmpty(configFilePath) 
			// get the default config file path	
			? (Path.Combine(Locations.DefaultConfigurationDirectory, DefaultFiles.DefaultConfigFile), false) 
			// if the user has specified a config file make it optional
			: (configFilePath, true);
	}
	
	public static IConfigurationBuilder AddEventStoreConfigFile(this IConfigurationBuilder builder, string path, bool optional = true) {
		if (string.IsNullOrWhiteSpace(path)) 
			throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));
		
		var yamlSource = new YamlConfigurationSource {
			Path           = path,
			Optional       = optional,
			ReloadOnChange = true
		};

		yamlSource.ResolveFileProvider();

		return builder.Add(yamlSource);
	}
}
