// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using static System.Environment;

namespace EventStore.Core.Configuration.Sources;

public class FallbackEnvironmentVariablesConfigurationProvider(IDictionary? environment = null) : ConfigurationProvider {
	IDictionary? Environment { get; } = environment;

	public override void Load() {
		var environment = Environment ?? GetEnvironmentVariables();

		var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

		foreach (var key in environment.Keys) {
			if (KurrentConfigurationKeys.TryNormalizeFallbackEnvVar(key, out var normalizedKey))
				data[normalizedKey] = environment[key]?.ToString();
		}

		Data = data;
	}
}
public class FallbackEnvironmentVariablesSource(IDictionary? environment = null) : IConfigurationSource {
	public IConfigurationProvider Build(IConfigurationBuilder builder) =>
		new FallbackEnvironmentVariablesConfigurationProvider(environment);
}

public static class FallbackEnvironmentVariablesConfigurationExtensions {
	public static IConfigurationBuilder AddFallbackEnvironmentVariables(this IConfigurationBuilder builder, IDictionary? environment = null) =>
		builder.Add(new FallbackEnvironmentVariablesSource(environment));

	public static IConfigurationBuilder AddFallbackEnvironmentVariables(this IConfigurationBuilder builder, Dictionary<string, string> environment) =>
		builder.Add(new FallbackEnvironmentVariablesSource(environment));

	public static IConfigurationBuilder AddFallbackEnvironmentVariables(this IConfigurationBuilder builder, params (string Key, string Value)[] environment) =>
		builder.Add(new FallbackEnvironmentVariablesSource(environment.ToDictionary(x => x.Key, x => x.Value)));
}
