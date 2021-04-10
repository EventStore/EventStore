using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace EventStore.Common.Configuration {
	public class Default : ConfigurationProvider {
		private readonly IEnumerable<KeyValuePair<string, object>> _defaultValues;

		public Default(IEnumerable<KeyValuePair<string, object>> defaultValues) {
			_defaultValues = defaultValues;
		}

		public override void Load() => Data = new Dictionary<string, string>(
			_defaultValues.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value?.ToString())));
	}
}
