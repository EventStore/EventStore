using System;
using System.IO;
using System.Threading;
using EventStore.Common.Exceptions;
using Serilog;

namespace EventStore.TestClient.Statistics {
	/// <summary>
	/// Csv logger configuration for the TestClient
	/// </summary>
	public class TestClientCsvLoggerConfiguration {
		private static int Initialized;
		private static readonly string outputTemplate = "{Message}{NewLine}";

		/// <summary>
		/// Initialize the csv logger
		/// </summary>
		/// <param name="logsDirectory"></param>
		/// <param name="componentName"></param>
		public static ILogger Initialize(string logsDirectory, string componentName) {
			if (Interlocked.Exchange(ref Initialized, 1) == 1) {
				throw new InvalidOperationException($"{nameof(Initialize)} may not be called more than once.");
			}

			if (logsDirectory.StartsWith("~")) {
				throw new ApplicationInitializationException(
					"The given log path starts with a '~'. Event Store does not expand '~'.");
			}

			var filename = Path.Combine(logsDirectory, $"{componentName}/log-stats.csv");
			return new LoggerConfiguration()
				.WriteTo.File(filename, outputTemplate: outputTemplate)
				.CreateLogger();
		}
	}
}
