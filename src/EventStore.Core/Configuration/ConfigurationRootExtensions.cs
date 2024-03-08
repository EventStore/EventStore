#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using EventStore.Common.Configuration;
using EventStore.Common.Exceptions;
using EventStore.Core.Configuration.Sources;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration;

public static class ConfigurationRootExtensions {
	public static string? CheckProvidersForEnvironmentVariables(this IConfigurationRoot? configurationRoot, IEnumerable<Type> optionSections) {
		if (configurationRoot == null)
			return null;

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

			var errorDescriptions =
				from key in provider.GetChildKeys()
				from property in environmentOptionsOnly
				where string.Equals(property.Name, key, StringComparison.CurrentCultureIgnoreCase)
				select property.GetCustomAttribute<EnvironmentOnlyAttribute>()?.Message;

			errorBuilder.Append(
				errorDescriptions
					.Aggregate(
						new StringBuilder(),
						(sb, err) => sb.AppendLine($"Provided by: {ClusterVNodeOptions.GetSourceDisplayName(provider.GetType())}. {err}")
					)
			);
		}

		return errorBuilder.Length != 0 ? errorBuilder.ToString() : null;
	}

	public static T BindOptions<T>(this IConfiguration configuration) where T : new() {
		try {
			return configuration.Get<T>() ?? new T();
		} catch (InvalidOperationException ex) {
			var messages = new string?[] { ex.Message, ex.InnerException?.Message }
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.Select(x => x?.TrimEnd('.'));

			throw new InvalidConfigurationException(string.Join(". ", messages) + ".");
		}
	}
}
