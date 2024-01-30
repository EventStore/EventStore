#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using EventStore.Common.Configuration;
using EventStore.Core.Configuration.Sources;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration;

public static class ConfigurationRootExtensions {
	public static string? CheckProvidersForEnvironmentVariables(this IConfigurationRoot? configurationRoot, IEnumerable<Type> optionSections) {
		if (configurationRoot == null) return null;

		var environmentOptionsOnly = optionSections.SelectMany(section => section.GetProperties())
			.Where(option => option.GetCustomAttribute<EnvironmentOnlyAttribute>() != null)
			.Select(option => option)
			.ToArray();

		var errorBuilder = new StringBuilder();

		foreach (var provider in configurationRoot.Providers) {
			var source = provider.GetType();

			if (source == typeof(EventStoreDefaultValuesConfigurationProvider) ||
			    source == typeof(EventStoreEnvironmentVariablesConfigurationProvider))
				continue;

			var errorDescriptions = from key in provider.GetChildKeys()
				from property in environmentOptionsOnly
				where string.Equals(property.Name, key, StringComparison.CurrentCultureIgnoreCase)
				select property.GetCustomAttribute<EnvironmentOnlyAttribute>()?.Message;

			errorBuilder.Append(
				errorDescriptions
					.Aggregate(
						new StringBuilder(),
						(sb, err) => sb.AppendLine($"Provided by: {provider.GetType().Name}. {err}")
					)
			);
		}

		return errorBuilder.Length != 0 ? errorBuilder.ToString() : null;
	}

	public static string GetString(this IConfiguration configurationRoot, string key) {
		return configurationRoot.GetValue<string>(key) ?? string.Empty;
	}

	public static T BindOptions<T>(this IConfiguration configuration) where T : new() =>
		configuration.Get<T>() ?? new T();
}
