using Serilog;

namespace EventStore.Common.Options {
	public interface IOptions {
		bool Help { get; }
		bool Version { get; }
		string Config { get; }
		string Log { get; }
		string LogConfig { get; }
		LogConsoleFormat LogConsoleFormat { get; }
		int LogFileSize { get; }
		RollingInterval LogFileInterval { get; }
		int LogFileRetentionCount { get; }
		bool DisableLogFile { get; }
		bool WhatIf { get; }
	}
}
