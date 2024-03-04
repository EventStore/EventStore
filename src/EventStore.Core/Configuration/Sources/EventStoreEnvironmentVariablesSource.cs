#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration.Sources {
	public class EventStoreEnvironmentVariablesSource(IDictionary? environment = null) : IConfigurationSource {
		private IDictionary? Environment { get; } = environment;

		public IConfigurationProvider Build(IConfigurationBuilder builder) =>
			new EventStoreEnvironmentVariablesConfigurationProvider(Environment);
	}

	public class EventStoreEnvironmentVariablesConfigurationProvider(IDictionary? environment = null) : ConfigurationProvider {
		private IDictionary Environment { get; } = environment ?? System.Environment.GetEnvironmentVariables();

		public override void Load() {
			var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
		
			var enumerator = Environment.GetEnumerator();
			try {
				while (enumerator.MoveNext()) {
					if (EventStoreConfigurationKeys.TryNormalizeEnvVar(enumerator.Entry.Key, out var normalizedKey)) 
						data[normalizedKey] = enumerator.Entry.Value?.ToString();
				}
			}
			finally {
				if (enumerator is IDisposable disposable)
					disposable.Dispose();
			}
		
			Data = data;
		}
	}

	public static class EventStoreEnvironmentVariablesConfigurationExtensions {
		public static IConfigurationBuilder AddEventStoreEnvironmentVariables(this IConfigurationBuilder builder, IDictionary? environment = null) =>
			builder.Add(new EventStoreEnvironmentVariablesSource(environment));
	
		public static IConfigurationBuilder AddEventStoreEnvironmentVariables(this IConfigurationBuilder builder, Dictionary<string, string> environment) =>
			builder.Add(new EventStoreEnvironmentVariablesSource(environment));
	
		public static IConfigurationBuilder AddEventStoreEnvironmentVariables(this IConfigurationBuilder builder, params (string Key, string Value)[] environment) =>
			builder.Add(new EventStoreEnvironmentVariablesSource(environment.ToDictionary(x => x.Key, x => x.Value)));
	
	
	}
}
