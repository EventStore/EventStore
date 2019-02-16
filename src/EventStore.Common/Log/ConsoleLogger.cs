using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace EventStore.Common.Log {
	public class ConsoleLogger : ILogger {
		public void Flush(TimeSpan? maxTimeToWait = null) {
		}

		public void Fatal(string format, params object[] args) {
			Console.WriteLine(Log("FATAL", format, args));
		}

		public void Error(string format, params object[] args) {
			Console.WriteLine(Log("ERROR", format, args));
		}

		public void Info(string format, params object[] args) {
			Console.WriteLine(Log("INFO ", format, args));
		}

		public void Debug(string format, params object[] args) {
			Console.WriteLine(Log("DEBUG", format, args));
		}

		public void Warn(string format, params object[] args) {
			Console.WriteLine(Log("WARN", format, args));
		}

		public void Trace(string format, params object[] args) {
			Console.WriteLine(Log("TRACE", format, args));
		}

		public void FatalException(Exception exc, string format, params object[] args) {
			Console.WriteLine(Log("FATAL", exc, format, args));
		}

		public void ErrorException(Exception exc, string format, params object[] args) {
			Console.WriteLine(Log("ERROR", exc, format, args));
		}

		public void InfoException(Exception exc, string format, params object[] args) {
			Console.WriteLine(Log("INFO ", exc, format, args));
		}

		public void DebugException(Exception exc, string format, params object[] args) {
			Console.WriteLine(Log("DEBUG", exc, format, args));
		}

		public void WarnException(Exception exc, string format, params object[] args) {
			Console.WriteLine(Log("WARN", exc, format, args));
		}

		public void TraceException(Exception exc, string format, params object[] args) {
			Console.WriteLine(Log("TRACE", exc, format, args));
		}

		private static readonly int ProcessId = Process.GetCurrentProcess().Id;

		private string Log(string level, string format, params object[] args) {
			return string.Format("[{0:00000},{1:00},{2:HH:mm:ss.fff},{3}] {4}",
				ProcessId,
				Thread.CurrentThread.ManagedThreadId,
				DateTime.UtcNow,
				level,
				args.Length == 0 ? format : string.Format(format, args));
		}

		private string Log(string level, Exception exc, string format, params object[] args) {
			var sb = new StringBuilder();
			while (exc != null) {
				sb.AppendLine();
				sb.AppendLine(exc.ToString());
				exc = exc.InnerException;
			}

			return string.Format("[{0:00000},{1:00},{2:HH:mm:ss.fff},{3}] {4}\nEXCEPTION(S) OCCURRED:{5}",
				ProcessId,
				Thread.CurrentThread.ManagedThreadId,
				DateTime.UtcNow,
				level,
				args.Length == 0 ? format : string.Format(format, args),
				sb);
		}
	}
}
