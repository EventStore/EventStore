using System;
using EventStore.Common.Log;
using Xunit.Abstractions;


namespace EventStore.ClientAPIAcceptanceTests {
	public class ClientApiLoggerBridge : EventStore.ClientAPI.ILogger {
		public static readonly ClientApiLoggerBridge Default =
			new ClientApiLoggerBridge(LogManager.GetLogger("client-api"));

		private readonly ILogger _log;

		public ClientApiLoggerBridge(ILogger log) {
			if (log == null) throw new ArgumentNullException(nameof(log));
			_log = log;
		}

		public void Error(string format, params object[] args) {
			if (args.Length == 0)
				_log.Error(format);
			else
				_log.Error(format, args);
		}

		public void Error(Exception ex, string format, params object[] args) {
			if (args.Length == 0)
				_log.ErrorException(ex, format);
			else
				_log.ErrorException(ex, format, args);
		}

		public void Info(string format, params object[] args) {
			if (args.Length == 0)
				_log.Info(format);
			else
				_log.Info(format, args);
		}

		public void Info(Exception ex, string format, params object[] args) {
			if (args.Length == 0)
				_log.InfoException(ex, format);
			else
				_log.InfoException(ex, format, args);
		}

		public void Debug(string format, params object[] args) {
			if (args.Length == 0)
				_log.Debug(format);
			else
				_log.Debug(format, args);
		}

		public void Debug(Exception ex, string format, params object[] args) {
			if (args.Length == 0)
				_log.DebugException(ex, format);
			else
				_log.DebugException(ex, format, args);
		}
	}

	internal class ConsoleLoggerBridge : EventStore.ClientAPI.ILogger {
		public static readonly ConsoleLoggerBridge Default = new ConsoleLoggerBridge();
		public void Error(string format, params object[] args) => Write("ERR", format, args);

		public void Error(Exception ex, string format, params object[] args) => Error($"{format} {ex}", args);

		public void Info(string format, params object[] args) => Write("INF", format, args);

		public void Info(Exception ex, string format, params object[] args) => Info($"{format} {ex}", args);

		public void Debug(string format, params object[] args)  => Write("DBG", format, args);

		public void Debug(Exception ex, string format, params object[] args) => Debug($"{format} {ex}", args);
		
		private void Write(string level, string format, params object[] args)
			=> Console.WriteLine($"[{level}] {format}", args);
	}
	internal class LoggerBridge : EventStore.ClientAPI.ILogger {
		private readonly ITestOutputHelper _testOutputHelper;

		public LoggerBridge(ITestOutputHelper testOutputHelper) {
			if (testOutputHelper == null) throw new ArgumentNullException(nameof(testOutputHelper));
			_testOutputHelper = testOutputHelper;
		}

		public void Error(string format, params object[] args) => Write("ERR", format, args);

		public void Error(Exception ex, string format, params object[] args) => Error($"{format} {ex}", args);

		public void Info(string format, params object[] args) => Write("INF", format, args);

		public void Info(Exception ex, string format, params object[] args) => Info($"{format} {ex}", args);

		public void Debug(string format, params object[] args)  => Write("DBG", format, args);

		public void Debug(Exception ex, string format, params object[] args) => Debug($"{format} {ex}", args);

		private void Write(string level, string format, params object[] args)
			=> _testOutputHelper.WriteLine($"[{level}] {format}", args);
	}
}
