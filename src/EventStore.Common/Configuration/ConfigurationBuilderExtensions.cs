using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

#nullable enable
namespace EventStore.Common.Configuration {
	public static class ConfigurationBuilderExtensions {
		public static IConfigurationBuilder AddEventStore(this IConfigurationBuilder configurationBuilder,
			string[] args, IDictionary environment, IEnumerable<KeyValuePair<string, object?>> defaultValues) {
			var builder = configurationBuilder
				.Add(new DefaultSource(defaultValues))
				.Add(new EnvironmentVariablesSource(environment))
				.Add(new CommandLineSource(args));

			var root = builder.Build(); // need to build twice as on disk config is configurable

			var configurationPath = root.GetValue<string>("Config");

			var configurationPathOptional = CompareConfigFilePath(defaultValues, configurationPath);
			var yamlSource = new YamlSource {
				Path = configurationPath,
				Optional = configurationPathOptional,
				ReloadOnChange = true
			};
			yamlSource.ResolveFileProvider();
			builder.Sources.Insert(1, yamlSource);

			return builder;
		}

		private static bool CompareConfigFilePath(IEnumerable<KeyValuePair<string, object?>> defaultValues, string? configurationPath) {
			var defaultValuesDictionary = defaultValues.ToDictionary(x => x.Key, x => x.Value);

			if (defaultValuesDictionary.TryGetValue("Config", out var defaultConfigurationPath)) {
				return string.Equals(configurationPath, defaultConfigurationPath?.ToString());
			}
			return true;
		}
	}
}
