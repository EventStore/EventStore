// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using Polly;
using Serilog;

namespace EventStore.Core.Resilience;

public static class ResilienceKeys {
	public static ResiliencePropertyKey<ILogger> Logger { get; } = new("logger");
}
