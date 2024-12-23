// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

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

			if (source == typeof(KurrentDefaultValuesConfigurationProvider) ||
				source == typeof(EventStoreEnvironmentVariablesConfigurationProvider))
				continue;

			var errorDescriptions =
				from key in provider.GetChildKeys()
				from property in environmentOptionsOnly
				where string.Equals(property.Name, key, StringComparison.CurrentCultureIgnoreCase)
				select (key, property.GetCustomAttribute<EnvironmentOnlyAttribute>()?.Message);

			errorBuilder.Append(
				errorDescriptions
					.Aggregate(
						new StringBuilder(),
						(sb, pair) => sb.AppendLine($"\"{pair.key}\" Provided by: {ClusterVNodeOptions.GetSourceDisplayName(EventStoreConfigurationKeys.Prefix + ":" + pair.key, provider)}. {pair.Message}")
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
