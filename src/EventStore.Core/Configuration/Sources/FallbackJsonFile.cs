// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Linq;
using EventStore.Common.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;
using static System.StringComparer;

namespace EventStore.Core.Configuration.Sources;

public class FallbackJsonFileConfigurationProvider(FallbackJsonFileConfigurationSource source)
	: JsonConfigurationProvider(source) {

	public override void Load() {
		base.Load();
		Data = Data.Keys.ToDictionary(
			KurrentConfigurationKeys.NormalizeFallback,
			x => Data[x], OrdinalIgnoreCase
		);
	}
}

public class FallbackJsonFileConfigurationSource : JsonConfigurationSource {
	public override IConfigurationProvider Build(IConfigurationBuilder builder) {
		EnsureDefaults(builder);
		return new FallbackJsonFileConfigurationProvider(this);
	}
}

public static class FallbackJsonFileConfigurationExtensions {
	public static IConfigurationBuilder AddFallbackJsonFile(this IConfigurationBuilder builder, Action<FallbackJsonFileConfigurationSource> configureSource) =>
		builder.Add(configureSource);

	public static IConfigurationBuilder AddFallbackConfigFile(this IConfigurationBuilder builder, string configFilePath,
		bool optional = false, bool reloadOnChange = false) {
		if (!Locations.TryLocateConfigFile(configFilePath, out var directory, out var fileName)) {
			if (optional)
				return builder;

			throw new FileNotFoundException(
				$"Could not find {configFilePath} in the following directories: {string.Join(", ", Locations.GetPotentialConfigurationDirectories())}");
		}

		builder.AddFallbackJsonFile(config => {
			config.Optional = optional;
			config.FileProvider = new PhysicalFileProvider(directory) {
				UseActivePolling = reloadOnChange,
				UsePollingFileWatcher = reloadOnChange
			};
			config.OnLoadException = context => Serilog.Log.Error(context.Exception, "err");
			config.Path = fileName;
			config.ReloadOnChange = reloadOnChange;
		});

		return builder;
	}

	public static IConfigurationBuilder AddFallbackConfigFiles(this IConfigurationBuilder builder, string pattern) {
		ConfigurationBuilderExtensions.AddConfigFiles("config", pattern, (file) =>
			builder.AddFallbackConfigFile(file, optional: true, reloadOnChange: true));
		return builder;
	}
}
