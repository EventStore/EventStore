using System;
using Microsoft.Extensions.Configuration;

#nullable enable
namespace EventStore.Common.Configuration {
	public static class ConfigurationRootExtensions {
		private static string[] INVALID_DELIMITERS = new[] { ";", "\t" };

		public static string[] GetCommaSeparatedValueAsArray(this IConfigurationRoot configurationRoot, string key) {
			string? value = configurationRoot.GetValue<string?>(key);
			if (string.IsNullOrEmpty(value)) {
				return Array.Empty<string>();
			}

			foreach (var invalidDelimiter in INVALID_DELIMITERS) {
				if (value.Contains(invalidDelimiter)) {
					throw new ArgumentException($"Invalid delimiter {invalidDelimiter} for {key}");
				}
			}

			return value.Split(',', StringSplitOptions.RemoveEmptyEntries);
		}
	}
}
