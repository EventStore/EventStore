// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using static System.StringComparer;

namespace EventStore.Core.Configuration.Sources;

public class FallbackCommandLineConfigurationProvider(IEnumerable<string> args)
	: CommandLineConfigurationProvider(args) {

	public override void Load() {
		base.Load();

		Data = Data.Keys
			.Where(KurrentConfigurationKeys.IsEventStoreKey)
			.ToDictionary(
				KurrentConfigurationKeys.NormalizeFallback,
				x => Data[x], OrdinalIgnoreCase
			);
	}
}

public class FallbackCommandLineConfigurationSource : IConfigurationSource {
	public FallbackCommandLineConfigurationSource(string[] args) {
		Args = args
			.Select(KurrentCommandLineConfigurationSource.NormalizeKeys)
			.Select((x, i) => KurrentCommandLineConfigurationSource.NormalizeBooleans(args, x, i));
	}

	private IEnumerable<string> Args { get; set; }

	public IConfigurationProvider Build(IConfigurationBuilder builder) =>
		new FallbackCommandLineConfigurationProvider(Args);
}

public static class FallbackCommandLineConfigurationExtensions {
	public static IConfigurationBuilder AddFallbackCommandLine(this IConfigurationBuilder builder, params string[] args) =>
		builder.Add(new FallbackCommandLineConfigurationSource(args));
}
