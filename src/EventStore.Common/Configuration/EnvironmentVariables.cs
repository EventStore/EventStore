using System;
using System.Collections;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace EventStore.Common.Configuration {
	internal class EnvironmentVariables : ConfigurationProvider {
		private readonly IDictionary _environment;
		private readonly string _prefix;

		public EnvironmentVariables(string prefix, IDictionary environment) {
			if (environment == null) {
				throw new ArgumentNullException(nameof(environment));
			}

			_prefix = $"{prefix}_";
			_environment = environment;
		}

		public override void Load() {
			Data = (from entry in _environment.OfType<DictionaryEntry>()
					let key = (string)entry.Key
					where key.StartsWith(_prefix)
					select new {
						key = StringExtensions.Computerize(key.Remove(0, _prefix.Length)),
						value = (string)entry.Value
					})
				.ToDictionary(x => x.key, x => x.value);
		}
	}
}
