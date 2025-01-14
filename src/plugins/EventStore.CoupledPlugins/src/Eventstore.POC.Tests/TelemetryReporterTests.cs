// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

using EventStore.POC.ConnectorsEngine;
using EventStore.POC.ConnectorsEngine.Processing;
using EventStore.POC.ConnectorsPlugin;
using FluentAssertions;

namespace Eventstore.POC.Tests;

public class TelemetryReporterTests {
	private static ConnectorState CreateConnectorState(string name, bool enabled, string sink) =>
		new(Id: name,
			Filter: "",
			Sink: sink,
			Affinity: NodeState.Leader,
			Enabled: enabled);

	[Fact]
	public async Task disabled() {
		var result = await TelemetryReporter.CollectTelemetryAsync(
			enabled: false,
			async () => [
				CreateConnectorState("connectorA", true, "console://"),
			],
			async () => []);

		result.Should().Equal(new Dictionary<string, object?>() {
			{ "enabled", false },
		});
	}

	[Fact]
	public async Task enabled() {
		var result = await TelemetryReporter.CollectTelemetryAsync(
			enabled: true,
			async () => [
				CreateConnectorState("console1", enabled: false, "console://"),
				CreateConnectorState("console2", true, "console://"),

				CreateConnectorState("http1", true, "https://localhost"),
				CreateConnectorState("http2", true, "http://localhost"),

				CreateConnectorState("unknown1", true, "somethingelse://"),
				CreateConnectorState("unknown2", true, "some nonsense"),
			],
			async () => ["console1"]);

		result.Should().Equal(new Dictionary<string, object?>() {
			{ "enabled", true },
			{ "connectorsActiveOnNode", 1 },
			{ "connectorsEnabledTotal", 5 },
			{ "connectorsEnabledConsoleSink", 1 },
			{ "connectorsEnabledHttpSink", 2 },
			{ "connectorsEnabledUnknownSink", 2 },
		});
	}
}
