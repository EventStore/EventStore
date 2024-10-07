// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration.Sources;

public static class EventStoreEnvironmentVariablesConfigurationExtensions {
	public static IConfigurationBuilder AddEventStoreEnvironmentVariables(this IConfigurationBuilder builder, IDictionary? environment = null) =>
		builder.Add(new EventStoreEnvironmentVariablesSource(environment));

	public static IConfigurationBuilder AddEventStoreEnvironmentVariables(this IConfigurationBuilder builder, Dictionary<string, string> environment) =>
		builder.Add(new EventStoreEnvironmentVariablesSource(environment));

	public static IConfigurationBuilder AddEventStoreEnvironmentVariables(this IConfigurationBuilder builder, params (string Key, string Value)[] environment) =>
		builder.Add(new EventStoreEnvironmentVariablesSource(environment.ToDictionary(x => x.Key, x => x.Value)));
}
