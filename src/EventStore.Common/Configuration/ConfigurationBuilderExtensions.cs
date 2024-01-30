using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EventStore.Common.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

#nullable enable
namespace EventStore.Common.Configuration {
	public static class ConfigurationBuilderExtensions {
		public static IConfigurationBuilder AddEventStore(this IConfigurationBuilder configurationBuilder,
			string configFileKey,
			string[] args, 
			IDictionary environment, 
			IEnumerable<KeyValuePair<string, object?>> defaultValues) {
			
			var builder = configurationBuilder
				.Add(new DefaultSource(defaultValues))
				.Add(new EnvironmentVariablesSource(environment))
				.Add(new CommandLineSource(args));

			var root = builder.Build(); // need to build twice as on disk config is configurable
			
			var yamlSource = new YamlSource {
				Path = root.GetString(configFileKey),
				Optional = !root.IsUserSpecified(configFileKey),
				ReloadOnChange = true
			};
			yamlSource.ResolveFileProvider();
			builder.Sources.Insert(1, yamlSource);

			return builder;
		}

		// Allows configuration to be mounted inside a specified section
		public static IConfigurationBuilder AddSection(
			this IConfigurationBuilder self,
			string sectionName,
			Action<IConfigurationBuilder> configure) =>
			
			self.Add(new SectionSource(sectionName, configure));

		public static IConfigurationBuilder AddEsdbConfigFile(
			this IConfigurationBuilder builder,
			string configFilePath,
			bool optional = false,
			bool reloadOnChange = false) {

			if (!Locations.TryLocateConfigFile(configFilePath, out var directory, out var fileName)) {
				if (optional) {
					return builder;
				}

				throw new FileNotFoundException(
					$"Could not find {configFilePath} in the following directories: {string.Join(", ", Locations.GetPotentialConfigurationDirectories())}");
			}

			builder.AddJsonFile(config => {
				config.Optional = optional;
				config.FileProvider = new PhysicalFileProvider(directory) {
					UseActivePolling = reloadOnChange,
					UsePollingFileWatcher = reloadOnChange
				};
				config.OnLoadException = context => Serilog.Log.Error(context.Exception, "err");
				config.Path = fileName;
				config.ReloadOnChange = reloadOnChange;
			});

			return builder;
		}

		public static IConfigurationBuilder AddEsdbConfigFiles(this IConfigurationBuilder builder, string subdirectory, string pattern) {
			// when searching for a file we check the directories in forward order until we find it
			// so when adding all the files we apply them in reverse order to keep the same precedence
			foreach (var directory in Locations.GetPotentialConfigurationDirectories().Reverse()) {
				var configDirectory = Path.Combine(directory, subdirectory);
				if (!Directory.Exists(configDirectory))
					continue;

				foreach (var configFile in Directory.EnumerateFiles(configDirectory, pattern).Order()) {
					builder.AddEsdbConfigFile(configFile, optional: true, reloadOnChange: true);
				}
			}

			return builder;
		}
	}
}
