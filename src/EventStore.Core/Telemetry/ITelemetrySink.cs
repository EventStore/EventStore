// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.Telemetry; 

public interface ITelemetrySink {
	Task Flush(JsonObject data, CancellationToken token);
}
