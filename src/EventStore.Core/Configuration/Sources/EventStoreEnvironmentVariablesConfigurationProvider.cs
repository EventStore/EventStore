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
}
