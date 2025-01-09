// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration.CommandLine;
using static System.StringComparer;

namespace EventStore.Core.Configuration.Sources;

public class KurrentDBCommandLineConfigurationProvider(IEnumerable<string> args)
	: CommandLineConfigurationProvider(args) {

	public override void Load() {
		base.Load();

		Data = Data.Keys
			.ToDictionary(
				KurrentConfigurationKeys.Normalize,
				x => Data[x], OrdinalIgnoreCase
			);
	}
}
