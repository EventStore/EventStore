using System;

namespace EventStore.Common.Log {
	public interface ILogger {
		void Flush(TimeSpan? maxTimeToWait = null);

		[StringFormatMethod("format")]
		void Fatal(string format, params object[] args);

		[StringFormatMethod("format")]
		void Error(string format, params object[] args);

		[StringFormatMethod("format")]
		void Info(string format, params object[] args);

		[StringFormatMethod("format")]
		void Debug(string format, params object[] args);

		[StringFormatMethod("format")]
		void Warn(string format, params object[] args);

		[StringFormatMethod("format")]
		void Trace(string format, params object[] args);

		[StringFormatMethod("format")]
		void FatalException(Exception exc, string format, params object[] args);

		[StringFormatMethod("format")]
		void ErrorException(Exception exc, string format, params object[] args);

		[StringFormatMethod("format")]
		void InfoException(Exception exc, string format, params object[] args);

		[StringFormatMethod("format")]
		void DebugException(Exception exc, string format, params object[] args);

		[StringFormatMethod("format")]
		void WarnException(Exception exc, string format, params object[] args);

		[StringFormatMethod("format")]
		void TraceException(Exception exc, string format, params object[] args);
	}
}
