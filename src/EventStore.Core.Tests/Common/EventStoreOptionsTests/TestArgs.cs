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
		public string LogLevel { get; set; }
		public LogConsoleFormat LogConsoleFormat { get; set; }
		public int LogFileSize { get; set; }
		public RollingInterval LogFileInterval { get; set; }
		public int LogFileRetentionCount { get; set; }
		public bool DisableLogFile { get; set; }
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
