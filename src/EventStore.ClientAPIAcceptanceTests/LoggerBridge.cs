using System;
using Xunit.Abstractions;


namespace EventStore.ClientAPI.Tests {
	internal class ConsoleLoggerBridge : ILogger {
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
