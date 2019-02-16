using System;
using System.Text;
using System.Threading;

namespace EventStore.ClientAPI.Common.Log {
	/// <summary>
	/// Implementation of <see cref="ILogger"/> which outputs to <see cref="Console"/>.
	/// </summary>
	public class ConsoleLogger : ILogger {
		/// <summary>
		/// Writes an error to the logger
		/// </summary>
		/// <param name="format">Format string for the log message.</param>
		/// <param name="args">Arguments to be inserted into the format string.</param>
		public void Error(string format, params object[] args) {
			Console.WriteLine(Log("ERROR", format, args));
		}

		/// <summary>
		/// Writes an error to the logger
		/// </summary>
		/// <param name="ex">A thrown exception.</param>
		/// <param name="format">Format string for the log message.</param>
		/// <param name="args">Arguments to be inserted into the format string.</param>
		public void Error(Exception ex, string format, params object[] args) {
			Console.WriteLine(Log("ERROR", ex, format, args));
		}

		/// <summary>
		/// Writes a debug message to the logger
		/// </summary>
		/// <param name="format">Format string for the log message.</param>
		/// <param name="args">Arguments to be inserted into the format string.</param>
		public void Debug(string format, params object[] args) {
			Console.WriteLine(Log("DEBUG", format, args));
		}

		/// <summary>
		/// Writes a debug message to the logger
		/// </summary>
		/// <param name="ex">A thrown exception.</param>
		/// <param name="format">Format string for the log message.</param>
		/// <param name="args">Arguments to be inserted into the format string.</param>
		public void Debug(Exception ex, string format, params object[] args) {
			Console.WriteLine(Log("DEBUG", ex, format, args));
		}

		/// <summary>
		/// Writes an information message to the logger
		/// </summary>
		/// <param name="format">Format string for the log message.</param>
		/// <param name="args">Arguments to be inserted into the format string.</param>
		public void Info(string format, params object[] args) {
			Console.WriteLine(Log("INFO", format, args));
		}

		/// <summary>
		/// Writes an information message to the logger
		/// </summary>
		/// <param name="ex">A thrown exception.</param>
		/// <param name="format">Format string for the log message.</param>
		/// <param name="args">Arguments to be inserted into the format string.</param>
		public void Info(Exception ex, string format, params object[] args) {
			Console.WriteLine(Log("INFO", ex, format, args));
		}

		private string Log(string level, string format, params object[] args) {
			return string.Format("[{0:00},{1:HH:mm:ss.fff},{2}] {3}",
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

			return string.Format("[{0:00},{1:HH:mm:ss.fff},{2}] {3}\nEXCEPTION(S) OCCURRED:{4}",
				Thread.CurrentThread.ManagedThreadId,
				DateTime.UtcNow,
				level,
				args.Length == 0 ? format : string.Format(format, args),
				sb);
		}
	}
}
