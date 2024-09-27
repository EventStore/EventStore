using System.Collections.Generic;
using Serilog;
using Serilog.Events;

namespace EventStore.Core.Tests.Fakes;

public class FakeLogger : ILogger {
	public List<LogEvent> LogMessages { get; } = new();

	public void Write(LogEvent logEvent) {
		LogMessages.Add(logEvent);
	}
}
