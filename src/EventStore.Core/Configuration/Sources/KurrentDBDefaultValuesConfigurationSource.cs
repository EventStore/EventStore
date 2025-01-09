// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using static System.StringComparer;

namespace EventStore.Core.Configuration.Sources;

public class KurrentDBDefaultValuesConfigurationSource(IEnumerable<KeyValuePair<string, string?>>? initialData = null) : IConfigurationSource {
	private IEnumerable<KeyValuePair<string, string?>> InitialData { get; } =
		initialData ?? new Dictionary<string, string?>();

	public IConfigurationProvider Build(IConfigurationBuilder builder) =>
		new KurrentDefaultValuesConfigurationProvider(InitialData);
}

public class KurrentDefaultValuesConfigurationProvider(IEnumerable<KeyValuePair<string, string?>> initialData)
	: MemoryConfigurationProvider(new(){
		InitialData = initialData.ToDictionary(
			kvp => $"{KurrentConfigurationKeys.Prefix}:{kvp.Key}",
			kvp => kvp.Value,
			OrdinalIgnoreCase)
	}) {
}

public static class EventStoreDefaultValuesConfigurationExtensions {
	public static IConfigurationBuilder AddKurrentDefaultValues(this IConfigurationBuilder configurationBuilder,
		IEnumerable<KeyValuePair<string, string?>> initialData) =>
		configurationBuilder.Add(new KurrentDBDefaultValuesConfigurationSource(initialData));

	public static IConfigurationBuilder AddKurrentDefaultValues(this IConfigurationBuilder builder,
		IEnumerable<KeyValuePair<string, object?>>? initialData) =>
		initialData is not null
			? builder.AddKurrentDefaultValues(initialData
				.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value?.ToString())).ToArray())
			: builder;

	public static IConfigurationBuilder AddKurrentDefaultValues(this IConfigurationBuilder builder) =>
		builder.AddKurrentDefaultValues(ClusterVNodeOptions.DefaultValues);
}
