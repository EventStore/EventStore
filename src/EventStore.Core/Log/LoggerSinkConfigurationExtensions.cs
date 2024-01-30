using System;
using Serilog;
using Serilog.Configuration;
using Serilog.Formatting;

namespace EventStore.Common.Log {
	internal static class LoggerSinkConfigurationExtensions {
		public static LoggerConfiguration RollingFile(this LoggerSinkConfiguration configuration, string logFileName,
			ITextFormatter expressionTemplate, int retainedFileCountLimit = 31,
			RollingInterval rollingInterval = RollingInterval.Day, int fileSizeLimitBytes = 1024 * 1024 * 1024) {
			if (configuration == null) throw new ArgumentNullException(nameof(configuration));

			return configuration.File(
				expressionTemplate,
				logFileName,
				buffered: false,
				rollOnFileSizeLimit: true,
				rollingInterval: rollingInterval,
				retainedFileCountLimit: retainedFileCountLimit,
				fileSizeLimitBytes: fileSizeLimitBytes);
		}
	}
}
