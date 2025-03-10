// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System;

namespace EventStore.POC.ConnectorsEngine.Processing.Activation;

public interface IActivationPolicy {
	IReadOnlySet<string> CalculateActive(
		Guid nodeId,
		NodeState nodeState,
		IDictionary<Guid, NodeState> members,
		IDictionary<string, ConnectorState> connectors);
}
