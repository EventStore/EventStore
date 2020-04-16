using System.Collections;
using System.Collections.Generic;
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

			return builder.Add<YamlSource>(s => {
				s.Path = configurationPath;
				s.Optional = true;
				s.ReloadOnChange = true;
				s.ResolveFileProvider();
			});
		}
	}
}
