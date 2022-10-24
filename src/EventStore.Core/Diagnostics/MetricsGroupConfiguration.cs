using System.Collections.Generic;

namespace EventStore.Core.Diagnostics {
	public class MetricsGroupConfiguration {
		private readonly Dictionary<string, string> _configuration = new();

		public void Add(string key, string value) => _configuration.Add(key, value);

		public bool TryGetValue(string key, out string value) => _configuration.TryGetValue(key, out value);
	}
}
