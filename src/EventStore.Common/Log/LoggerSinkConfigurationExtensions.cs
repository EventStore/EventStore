using System;
using Serilog;
using Serilog.Configuration;
using Serilog.Formatting.Compact;

namespace EventStore.Common.Log {
	internal static class LoggerSinkConfigurationExtensions {
		public static LoggerConfiguration RollingFile(this LoggerSinkConfiguration configuration, string logFileName) {
			if (configuration == null)
				throw new ArgumentNullException(nameof(configuration));

			return configuration.File(
				new CompactJsonFormatter(),
				logFileName,
				buffered: false,
				rollOnFileSizeLimit: true);
		}
	}
}
