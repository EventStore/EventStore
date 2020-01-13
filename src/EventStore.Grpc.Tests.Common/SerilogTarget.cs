using System;
using System.Linq;
using NLog;
using NLog.Targets;
using Serilog;

namespace EventStore.Grpc {
	public sealed class SerilogTarget : TargetWithLayout {
		protected override void Write(LogEventInfo logEvent) {
			var log = Log.ForContext(Serilog.Core.Constants.SourceContextPropertyName, logEvent.LoggerName);
			var logEventLevel = ConvertLevel(logEvent.Level);
			if ((logEvent.Parameters?.Length ?? 0) == 0) {
				// NLog treats a single string as a verbatim string; Serilog treats it as a String.Format format and hence collapses doubled braces
				// This is the most direct way to emit this without it being re-processed by Serilog (via @nblumhardt)
				var template = new Serilog.Events.MessageTemplate(new[]
					{new Serilog.Parsing.TextToken(logEvent.FormattedMessage)});
				log.Write(new Serilog.Events.LogEvent(DateTimeOffset.Now, logEventLevel, logEvent.Exception, template,
					Enumerable.Empty<Serilog.Events.LogEventProperty>()));
			} else
				// Risk: tunneling an NLog format and assuming it will Just Work as a Serilog format
#pragma warning disable Serilog004 // Constant MessageTemplate verifier
				log.Write(logEventLevel, logEvent.Exception, logEvent.Message, logEvent.Parameters);
#pragma warning restore Serilog004
		}

		static Serilog.Events.LogEventLevel ConvertLevel(LogLevel logEventLevel) {
			if (logEventLevel == LogLevel.Info)
				return Serilog.Events.LogEventLevel.Information;
			else if (logEventLevel == LogLevel.Trace)
				return Serilog.Events.LogEventLevel.Verbose;
			else if (logEventLevel == LogLevel.Debug)
				return Serilog.Events.LogEventLevel.Debug;
			else if (logEventLevel == LogLevel.Error)
				return Serilog.Events.LogEventLevel.Error;
			return Serilog.Events.LogEventLevel.Fatal;
		}
	}
}
