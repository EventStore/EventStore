using EventStore.Common.Options;
using System.Net;
using Serilog;

namespace EventStore.Core.Tests.Common {
	public class TestArgs : IOptions {
		public bool Help { get; set; }
		public bool Version { get; set; }
		public string Config { get; set; }
		public string Log { get; set; }
		public string LogConfig { get; set; }
		public string LogLevel { get; }
		public LogConsoleFormat LogConsoleFormat { get; }
		public int LogFileSize { get; }
		public RollingInterval LogFileInterval { get; }
		public int LogFileRetentionCount { get; }
		public bool DisableLogFile { get; }
		public IPEndPoint[] GossipSeed { get; set; }
		public bool WhatIf { get; set; }
		public ProjectionType RunProjections { get; set; }

		public int HttpPort { get; set; }

		public TestArgs() {
			HttpPort = 2112;
			Log = "~/logs";
		}
	}
}
