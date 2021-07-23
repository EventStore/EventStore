using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;

#nullable enable
namespace EventStore.Common.Configuration {
	public static class ConfigurationBuilderExtensions {
		public static IConfigurationBuilder AddEventStore(this IConfigurationBuilder configurationBuilder,
			string[] args, IDictionary environment, IEnumerable<KeyValuePair<string, object?>> defaultValues,
			string defaultConfigurationPath) {
			var builder = configurationBuilder
				.Add(new DefaultSource(defaultValues))
				.Add(new EnvironmentVariablesSource(environment))
				.Add(new CommandLineSource(args));

			var root = builder.Build(); // need to build twice as on disk config is configurable

			var configurationPath = root.GetValue<string>("Config");

			var yamlSource = new YamlSource {
				Path = configurationPath,
				Optional = configurationPath == defaultConfigurationPath,
				ReloadOnChange = true
			};
			yamlSource.ResolveFileProvider();
			builder.Sources.Insert(1, yamlSource);

			return builder;
		}
	}
}
