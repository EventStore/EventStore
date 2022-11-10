using System;
using Microsoft.Extensions.Configuration;

#nullable enable
namespace EventStore.Common.Configuration {
	public static class ConfigurationRootExtensions {
		public static string[] GetCommaSeparatedValueAsArray(this IConfigurationRoot configurationRoot, string key) =>
			configurationRoot.GetValue<string?>(key)?.Split(',', StringSplitOptions.RemoveEmptyEntries) ??
			Array.Empty<string>();
	}
}
