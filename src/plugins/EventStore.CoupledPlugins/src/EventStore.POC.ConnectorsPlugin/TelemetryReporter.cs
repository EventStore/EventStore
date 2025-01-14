// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.POC.ConnectorsEngine.Processing;

namespace EventStore.POC.ConnectorsPlugin;

public class TelemetryReporter {
	public static async Task<Dictionary<string, object?>> CollectTelemetryAsync(
		bool enabled,
		Func<Task<ConnectorState[]>> getConnectors,
		Func<Task<string[]>> getActiveConnectorsOnNode) {

		var properties = new Dictionary<string, object?> {
			["enabled"] = enabled,
		};

		if (!enabled)
			return properties;

		var connectorStates = await getConnectors();
		var activeConnectors = await getActiveConnectorsOnNode();

		var sinkTypeCounts = new Dictionary<string, int>();
		int connectorsEnabled = 0;

		foreach (var connectorState in connectorStates) {
			if (!connectorState.Enabled)
				continue;

			var sinkType = SinkFactory.DetectSinkType(connectorState.Sink);
			var count = sinkTypeCounts.TryGetValue(sinkType, out var x) ? x : 0;
			sinkTypeCounts[sinkType] = count + 1;

			connectorsEnabled++;
		}

		properties["connectorsActiveOnNode"] = activeConnectors.Length;
		properties["connectorsEnabledTotal"] = connectorsEnabled;

		foreach (var kvp in sinkTypeCounts.OrderBy(kvp => kvp.Key))
			properties[$"connectorsEnabled{kvp.Key}Sink"] = kvp.Value;

		return properties;
	}
}
