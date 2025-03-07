// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.POC.ConnectorsEngine.Processing.Sinks;
using EventStore.POC.IO.Core;
using Serilog;

namespace EventStore.POC.ConnectorsEngine.Processing;

public static class SinkFactory {
	public static ISink Create(string connectorId, Uri config, ILogger logger) {
		return config.Scheme switch {
			"http" => new HttpSink(connectorId, config, logger),
			"https" => new HttpSink(connectorId, config, logger),
			"console" => new ConsoleSink(connectorId, logger),
			_ => throw new Exception($"Sink of type '{config.Scheme}' is not supported!")
		};
	}

	public static string DetectSinkType(string sink) {
		try {
			return new UriBuilder(sink).Scheme switch {
				"http" => "Http",
				"https" => "Http",
				"console" => "Console",
				_ => "Unknown",
			};
		} catch {
			return "Unknown";
		}
	}
}
