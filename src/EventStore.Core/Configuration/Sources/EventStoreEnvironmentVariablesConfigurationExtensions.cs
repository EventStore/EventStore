#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration.Sources {
	public static class EventStoreEnvironmentVariablesConfigurationExtensions {
		public static IConfigurationBuilder AddEventStoreEnvironmentVariables(this IConfigurationBuilder builder, IDictionary? environment = null) =>
			builder.Add(new EventStoreEnvironmentVariablesSource(environment));
	
		public static IConfigurationBuilder AddEventStoreEnvironmentVariables(this IConfigurationBuilder builder, Dictionary<string, string> environment) =>
			builder.Add(new EventStoreEnvironmentVariablesSource(environment));
	
		public static IConfigurationBuilder AddEventStoreEnvironmentVariables(this IConfigurationBuilder builder, params (string Key, string Value)[] environment) =>
			builder.Add(new EventStoreEnvironmentVariablesSource(environment.ToDictionary(x => x.Key, x => x.Value)));
	}
}