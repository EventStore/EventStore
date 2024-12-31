// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using static System.String;
using static System.StringComparison;

namespace EventStore.Core.Configuration.Sources;

public static class KurrentConfigurationKeys {
	public const string Prefix = "Kurrent";
	public const string FallbackPrefix = "EventStore";

	private const string EnvVarKeyDelimiter = "__";
	private const string ArgWordDelimiter = "-";
	private const string EnvVarWordDelimiter = "_";

	private static readonly char[] Delimiters = { '-', '_', ':' };

	private static readonly IReadOnlyList<string> SectionKeys;
	private static readonly IReadOnlyList<string> OptionsKeys;
	private static readonly IReadOnlyList<string> AllKnownKeys;

	static KurrentConfigurationKeys() {
		SectionKeys = typeof(ClusterVNodeOptions).GetProperties()
			.Where(prop => prop.GetCustomAttribute<ClusterVNodeOptions.OptionGroupAttribute>() != null)
			.Select(property => property.Name)
			.ToList();

		OptionsKeys = typeof(ClusterVNodeOptions).GetProperties()
			.Where(prop => prop.GetCustomAttribute<ClusterVNodeOptions.OptionGroupAttribute>() != null)
			.SelectMany(
				property => property.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
			.Select(x => x.Name)
			.ToList();

		AllKnownKeys = SectionKeys.Concat(OptionsKeys).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
	}

	// outputs a key for IConfiguration e.g. Kurrent:StreamInfoCacheCapacity
	public static string Normalize(string key) => Normalize(Prefix, Prefix, key);
	public static string NormalizeFallback(string key) => Normalize(FallbackPrefix, Prefix, key);

	public static string Normalize(string originalPrefix, string targetPrefix, string key) {
		// if the key doesn't contain any delimiters,
		// we can just get out, transforming the key
		// if needed because of cli lowercase args
		if (!key.Any(Delimiters.Contains))
			return $"{targetPrefix}:{Transform(key)}";

		// remove the prefix and normalize the key
		var keys = (key.StartsWith(originalPrefix, OrdinalIgnoreCase)
				? key.Remove(0, originalPrefix.Length)
				: key)
			.Replace(EnvVarKeyDelimiter, ConfigurationPath.KeyDelimiter)
			.Replace(ArgWordDelimiter, EnvVarWordDelimiter)
			.Split(ConfigurationPath.KeyDelimiter)
			.Where(x => x.Length > 0)
			.Select(Transform);

		return $"{targetPrefix}:{Join(ConfigurationPath.KeyDelimiter, keys)}";

		// because we know all keys, we can ensure the name is always correct
		static string Transform(string key) {
			var value = Pascalize(key);
			return AllKnownKeys.FirstOrDefault(x => x.Equals(value, OrdinalIgnoreCase)) ?? value;
		}

		static string Pascalize(string key) =>
			Join(Empty, key
				.ToLowerInvariant()
				.Split(EnvVarWordDelimiter)
				.Select(word => new string(word.Select((c, i) => i == 0 ? char.ToUpper(c) : c).ToArray()))
			);
	}

	/// <summary>
	/// Determines if the given key is an EventStore key
	/// </summary>
	public static bool IsEventStoreKey(string? key) =>
		key is not null && key.StartsWith($"{FallbackPrefix}", OrdinalIgnoreCase);

	/// <summary>
	/// Determines if the given key starts with the given prefix
	/// </summary>
	private static bool IsEnvVarWithPrefix(string prefix, string? key) =>
		key is not null && key.StartsWith($"{prefix}{EnvVarWordDelimiter}", OrdinalIgnoreCase);

	/// <summary>
	/// Only normalizes the given key if it starts with the given prefix
	/// </summary>
	private static bool TryNormalizeEnvVar(string originalPrefix, string targetPrefix, string key, [MaybeNullWhen(false)] out string normalizedKey) =>
		(normalizedKey = IsEnvVarWithPrefix(originalPrefix, key) ? Normalize(originalPrefix, targetPrefix, key) : null) is not null;

	/// <summary>
	/// Only normalizes the given key if it is an Event Store environment variable.
	/// </summary>
	public static bool TryNormalizeFallbackEnvVar(object? key, [MaybeNullWhen(false)] out string normalizedKey) =>
		TryNormalizeEnvVar(FallbackPrefix, Prefix, key?.ToString() ?? Empty, out normalizedKey);

	/// <summary>
	/// Only normalizes the given key if it is a Kurrent environment variable.
	/// </summary>
	public static bool TryNormalizeEnvVar(object? key, [MaybeNullWhen(false)] out string normalizedKey) =>
		TryNormalizeEnvVar(Prefix, Prefix, key?.ToString() ?? Empty, out normalizedKey);

	public static string StripConfigurationPrefix(string key) =>
		key.StartsWith($"{Prefix}:", OrdinalIgnoreCase) ? key.Remove(0, Prefix.Length + 1) : key;
}
