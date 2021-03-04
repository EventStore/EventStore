using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;

namespace EventStore.TestClient {
	/// <summary>
	/// Data contract for the command-line options accepted by test client.
	/// This contract is handled by CommandLine project for .NET
	/// </summary>
	public sealed class ClientOptions : IOptions {
		public bool Help { get; set; }

		public bool Version { get; set; }

		public string Log { get; set; }
		public string Config { get; set; }

		public bool WhatIf { get; set; }

		public string Host { get; set; }
		public int TcpPort { get; set; }
		public int HttpPort { get; set; }
		public int Timeout { get; set; }
		public int ReadWindow { get; set; }
		public int WriteWindow { get; set; }
		public int PingWindow { get; set; }
		public string[] Command { get; set; }
		public bool Reconnect { get; set; }

		public bool UseTls { get; set; }
		public bool TlsValidateServer { get; set; }

		public ClientOptions() {
			Config = "";
			Command = new string[] { };
			Help = Opts.ShowHelpDefault;
			Version = Opts.ShowVersionDefault;
			Log = Locations.DefaultTestClientLogDirectory;
			WhatIf = Opts.WhatIfDefault;
			Host = IPAddress.Loopback.ToString();
			TcpPort = 1113;
			HttpPort = 2113;
			Timeout = -1;
			ReadWindow = 2000;
			WriteWindow = 2000;
			PingWindow = 2000;
			Reconnect = true;
			UseTls = false;
			TlsValidateServer = false;
		}
	}
}
