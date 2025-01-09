// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration.Sources;

public class KurrentDBCommandLineConfigurationSource : IConfigurationSource {
	public KurrentDBCommandLineConfigurationSource(string[] args) {
		Args = args.Select(NormalizeKeys).Select((x, i) => NormalizeBooleans(args, x, i));
	}

	public static string NormalizeKeys(string x)  => x[0] == '-' && x[1] != '-' ? $"-{x}" : x;

	public static string NormalizeBooleans(string[] args, string x, int i) {
			if (!x.StartsWith("--"))
				return x;

			if (x.EndsWith('+'))
				return $"{x[..^1]}=true";

			if (x.EndsWith('-'))
				return $"{x[..^1]}=false";

			if (x.Contains('='))
				return x;

			if (i != args.Length - 1 && !args[i + 1].StartsWith("--"))
				return x;

			return $"{x}=true";
	}
	private IEnumerable<string> Args { get; set; }

	public IConfigurationProvider Build(IConfigurationBuilder builder) =>
		new KurrentDBCommandLineConfigurationProvider(Args);
}
