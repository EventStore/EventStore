#nullable enable

using System;
using Microsoft.Extensions.Configuration;

namespace EventStore.Common.Configuration;

public static class ConfigurationRootExtensions {
	static readonly string[] INVALID_DELIMITERS = [";", "\t"];

	public static string[] GetCommaSeparatedValueAsArray(this IConfiguration configuration, string key) {
		string? value = configuration.GetValue<string?>(key);
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
