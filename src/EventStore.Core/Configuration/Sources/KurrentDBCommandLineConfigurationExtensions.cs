// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration.Sources;

public static class KurrentDBCommandLineConfigurationExtensions {
	public static IConfigurationBuilder AddKurrentCommandLine(this IConfigurationBuilder builder, params string[] args) =>
		builder.Add(new KurrentDBCommandLineConfigurationSource(args));
}
