using System;
using System.Diagnostics;
using Amazon.Runtime.Internal.Util;
using Serilog.Events;

namespace EventStore.Core.Services.Archive.Storage.S3;

public class AmazonTraceSerilogger : TraceListener {
	private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext("SourceContext", "Amazon");

	public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id,
		object data) {
		Log(eventCache, source, eventType, id, data);
	}

	public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id,
		params object[] data) {
		Log(eventCache, source, eventType, id, data);
	}

	public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id) {
		Log(eventCache, source, eventType, id);
	}

	public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id,
		string format, params object[] args) {
		Log(eventCache, source, eventType, id, string.Format(format, args));
	}

	public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id,
		string message) {
		Log(eventCache, source, eventType, id, message);
	}

	public override void TraceTransfer(TraceEventCache eventCache, string source, int id, string message,
		Guid relatedActivityId) {
		Log(eventCache, source, TraceEventType.Transfer, id, relatedActivityId);
	}

	public override void Write(string message) {
		if (message is not null)
			TraceData(new TraceEventCache(), Name, TraceEventType.Information, 0, message);
	}

	public override void WriteLine(string message) {
		if (message is not null)
			TraceData(new TraceEventCache(), Name, TraceEventType.Information, 0, message);
	}

	private static void Log(TraceEventCache eventCache, string source, TraceEventType eventType, int eventId, params object[] data) {
		if (data.Length is 0)
			return;

		var logMessage = data[0] as LogMessage;
		var exception = data.Length > 1 ? data[1] as Exception : null;

		if (logMessage is null)
			return;

		var logLevel = GetLogLevel(eventType);
		Logger.Write(logLevel, exception, logMessage.Format, logMessage.Args);
	}

	private static LogEventLevel GetLogLevel(TraceEventType eventType) {
		return eventType switch {
			TraceEventType.Verbose => LogEventLevel.Verbose,
			TraceEventType.Information => LogEventLevel.Information,
			TraceEventType.Warning => LogEventLevel.Warning,
			TraceEventType.Error => LogEventLevel.Error,
			TraceEventType.Critical => LogEventLevel.Fatal,
			_ => LogEventLevel.Verbose
		};
	}
}
