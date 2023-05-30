using System;
using System.Collections.Generic;
using Serilog.Core;
using Serilog.Events;

namespace EventStore.Common.Log;

public class LogPublisher : ILogEventSink {
	private static readonly object _lock = new();
	public static readonly LogPublisher Instance = new();
	private readonly List<ILogEventSink> _sinks = new();

	public void Emit(LogEvent logEvent) {
		lock (_lock) {
			foreach (var sink in _sinks) {
				try {
					sink.Emit(logEvent);
				} catch {
					// ignored
				}
			}

		}
	}

	public void Register(ILogEventSink sink) {
		lock (_lock) {
			_sinks.Add(sink);
		}
	}
}
