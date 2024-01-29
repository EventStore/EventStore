#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.Configuration;
using static System.StringComparison;

namespace EventStore.Common.Configuration.Sources;

public static class EventStoreConfigurationKeys {
	public const string Prefix = "EventStore";
	
	const string EnvVarKeyDelimiter  = "__";
	const string ArgWordDelimiter    = "-";
	const string EnvVarWordDelimiter = "_";

	static readonly char[] Delimiters = { '-', '_', ':' };

	// public static readonly List<Type> Sections;
	// static EventStoreConfigurationKeys() {
	// 	Sections = typeof(ClusterVNodeOptions)
	// 		.GetProperties(BindingFlags.Public | BindingFlags.Instance)
	// 		.Where(p => p.GetCustomAttribute<OptionGroupAttribute>() != null)
	// 		.Select(p => p.PropertyType).ToList();
	// }

	public static string Normalize(string key) {
		// if the key doesn't contain any delimiters,
		// it is assumed that it's already normalized
		if (!key.Any(Delimiters.Contains))
			return $"{Prefix}:{key}";

		// remove the prefix and normalize the key 
		var keys = (key.StartsWith(Prefix, OrdinalIgnoreCase) ? key.Remove(0, Prefix.Length) : key)
			.Replace(EnvVarKeyDelimiter, ConfigurationPath.KeyDelimiter)
			.Replace(ArgWordDelimiter, EnvVarWordDelimiter)
			.Split(ConfigurationPath.KeyDelimiter)
			.Where(x => x.Length > 0)
			.Select(Pascalize);

		return $"{Prefix}:{String.Join(ConfigurationPath.KeyDelimiter, keys)}";

		static string Pascalize(string key) =>
			String.Join(String.Empty, key
				.ToLowerInvariant()
				.Split(EnvVarWordDelimiter)
				.Select(word => new string(word.Select((c, i) => i == 0 ? char.ToUpper(c) : c).ToArray()))
			);
	}
	
	/// <summary>
	/// Determines if the given key is an event store environment variable.
	/// </summary>
	public static bool IsEventStoreEnvVar(string? key) =>
		key?.StartsWith($"{Prefix}{EnvVarWordDelimiter}", OrdinalIgnoreCase) ?? false;
	
	/// <summary>
	/// Only normalizes the given key if it is an event store environment variable.
	/// </summary>
	public static bool TryNormalizeEnvVar(string key, [MaybeNullWhen(false)] out string normalizedKey) => 
		(normalizedKey = IsEventStoreEnvVar(key) ? Normalize(key) : null) is not null;
	
	/// <summary>
	/// Only normalizes the given key if it is an event store environment variable.
	/// </summary>
	public static bool TryNormalizeEnvVar(object? key, [MaybeNullWhen(false)] out string normalizedKey) => 
		TryNormalizeEnvVar(key?.ToString() ?? String.Empty, out normalizedKey);
	
	public static string StripConfigurationPrefix(string key) =>
		key.StartsWith($"{Prefix}:", OrdinalIgnoreCase) ? key.Remove(0, Prefix.Length + 1) : key;
}
