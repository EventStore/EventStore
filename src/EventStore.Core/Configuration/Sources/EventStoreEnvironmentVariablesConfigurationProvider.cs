#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration.Sources {
	public class EventStoreEnvironmentVariablesConfigurationProvider(IDictionary? environment = null) : ConfigurationProvider {
		private IDictionary Environment { get; } = environment ?? System.Environment.GetEnvironmentVariables();

		public override void Load() {
			var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

			foreach (var k in Environment.Keys) {
				if (EventStoreConfigurationKeys.TryNormalizeEnvVar(k, out var normalizedKey)) 
					data[normalizedKey] = Environment[k]?.ToString();
			}
		
			Data = data;
		}
	}
}
