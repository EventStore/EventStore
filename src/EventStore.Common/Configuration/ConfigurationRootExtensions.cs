using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;

#nullable enable
namespace EventStore.Common.Configuration {
	public static class ConfigurationRootExtensions {
		public static void Validate<TOptions>(this IConfigurationRoot configurationRoot) {
			var options = typeof(TOptions).GetProperties()
				.SelectMany(
					property => property.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
				.Select(x => x.Name)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			foreach (var (key, _) in configurationRoot.AsEnumerable()) {
				if (!options.Contains(key)) {
					throw new ArgumentException($"The option {key} is not a known option.", key);
				}
			}
		}

		public static string[] GetCommaSeparatedValueAsArray(this IConfigurationRoot configurationRoot, string key) =>
			configurationRoot.GetValue<string?>(key)?.Split(',', StringSplitOptions.RemoveEmptyEntries) ??
			Array.Empty<string>();
	}
}
