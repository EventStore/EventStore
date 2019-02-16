using EventStore.Common.Options;
using System.Net;

namespace EventStore.Core.Tests.Common {
	public class TestArgs : IOptions {
		public bool Help { get; set; }
		public bool Version { get; set; }
		public string Config { get; set; }
		public string Log { get; set; }
		public string[] Defines { get; set; }
		public IPEndPoint[] GossipSeed { get; set; }
		public bool WhatIf { get; set; }
		public bool Force { get; set; }
		public ProjectionType RunProjections { get; set; }

		public int HttpPort { get; set; }

		public TestArgs() {
			HttpPort = 2112;
			Log = "~/logs";
		}
	}
}
