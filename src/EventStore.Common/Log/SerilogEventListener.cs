using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Serilog.Events;

namespace EventStore.Common.Log {
	public class SerilogEventListener : EventListener {
		//qq can we get hold of the manifests in here and do things like interpret integer values as their enums?
		// we dont want to use the boxing overloads of Write in the EventSource ofc.

		private readonly Dictionary<string, LogEventLevel> _eventSources = new() {
			{ "eventstore-dev-certs", LogEventLevel.Verbose },
			{ "eventstore-experiments-grpc", LogEventLevel.Verbose }, //qq
			//{ "eventstore-experiments-core", LogEventLevel.Verbose }, //qq
			//{ "eventstore-experiments-projections", LogEventLevel.Verbose }, //qq
//			{ "eventstore-experiments-dynamic", LogEventLevel.Verbose }, //qq
		};

		protected override void OnEventSourceCreated(EventSource eventSource) {
			if (_eventSources.TryGetValue(eventSource.Name, out var level)) {
				var manifest = EventSource.GenerateManifest(eventSource.GetType(), default); //qq
				EnableEvents(eventSource, ConvertToEventSourceLevel(level));
			}
		}

		protected override void OnEventWritten(EventWrittenEventArgs eventData) {
			if (eventData.Message is null) return;
			Serilog.Log.Logger.Write(ConvertToSerilogLevel(eventData.Level), eventData.Message, eventData.Payload?.ToArray());
		}

		private static LogEventLevel ConvertToSerilogLevel(EventLevel level) {
			switch (level) {
				case EventLevel.Critical:
					return LogEventLevel.Fatal;
				case EventLevel.Error:
					return LogEventLevel.Error;
				case EventLevel.Informational:
					return LogEventLevel.Information;
				case EventLevel.Verbose:
					return LogEventLevel.Verbose;
				case EventLevel.Warning:
					return LogEventLevel.Warning;
				case EventLevel.LogAlways:
					return LogEventLevel.Information;
			}

			return LogEventLevel.Information;
		}
		private static EventLevel ConvertToEventSourceLevel(LogEventLevel level) {
			switch (level) {
				case LogEventLevel.Fatal:
					return EventLevel.Critical;
				case LogEventLevel.Error:
					return EventLevel.Error;
				case LogEventLevel.Information:
					return EventLevel.Informational;
				case LogEventLevel.Verbose:
					return EventLevel.Verbose;
				case LogEventLevel.Warning:
					return EventLevel.Warning;
			}

			return EventLevel.Informational;
		}
	}
}
