using System;

namespace EventStore.ClientAPI.Common.Log {
	class DebugLogger : ILogger {
		public void Error(string format, params object[] args) {
			System.Diagnostics.Trace.WriteLine(Log("ERROR", format, args));
		}

		public void Error(Exception ex, string format, params object[] args) {
			System.Diagnostics.Trace.WriteLine(Log("ERROR", ex, format, args));
		}

		public void Debug(string format, params object[] args) {
			System.Diagnostics.Trace.WriteLine(Log("DEBUG", format, args));
		}

		public void Debug(Exception ex, string format, params object[] args) {
			System.Diagnostics.Trace.WriteLine(Log("DEBUG", ex, format, args));
		}

		public void Info(string format, params object[] args) {
			System.Diagnostics.Trace.WriteLine(Log("INFO", format, args));
		}

		public void Info(Exception ex, string format, params object[] args) {
			System.Diagnostics.Trace.WriteLine(Log("INFO", ex, format, args));
		}


		private string Log(string level, string format, params object[] args) {
			return string.Format("{0}: {1}", level, args.Length == 0 ? format : string.Format(format, args));
		}

		private string Log(string level, Exception exc, string format, params object[] args) {
			return string.Format("{0} EXCEPTION: {1}\nException: {2}", level,
				args.Length == 0 ? format : string.Format(format, args), exc);
		}
	}
}
