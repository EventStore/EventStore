// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using Serilog;
using Serilog.Events;

namespace EventStore.Auth.UserCertificates.Tests;

public class FakeLogger : ILogger {
	public List<LogEvent> LogMessages { get; } = new();

	public void Write(LogEvent logEvent) {
		LogMessages.Add(logEvent);
	}
}
