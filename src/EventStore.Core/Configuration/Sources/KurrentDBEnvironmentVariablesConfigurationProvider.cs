// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using static System.Environment;

namespace EventStore.Core.Configuration.Sources;

public class KurrentDBEnvironmentVariablesConfigurationProvider(IDictionary? environment = null) : ConfigurationProvider {
	IDictionary? Environment { get; } = environment;

	public override void Load() {
		var environment = Environment ?? GetEnvironmentVariables();

		var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

		foreach (var key in environment.Keys) {
			if (KurrentConfigurationKeys.TryNormalizeEnvVar(key, out var normalizedKey))
				data[normalizedKey] = environment[key]?.ToString();
		}

		Data = data;
	}
}