using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;
using EventStore.Rags;

namespace EventStore.TestClient {
	/// <summary>
	/// Data contract for the command-line options accepted by test client.
	/// This contract is handled by CommandLine project for .NET
	/// </summary>
	public sealed class ClientOptions : IOptions {
		[ArgDescription(Opts.ShowHelpDescr)] public bool Help { get; set; }

		[ArgDescription(Opts.ShowVersionDescr)]
		public bool Version { get; set; }

		[ArgDescription(Opts.LogsDescr)] public string Log { get; set; }
		[ArgDescription(Opts.ConfigsDescr)] public string Config { get; set; }

		[ArgDescription(Opts.WhatIfDescr, Opts.AppGroup)]
		public bool WhatIf { get; set; }

		[ArgDescription(Opts.HostDescr)] public string Host { get; set; }
		[ArgDescription(Opts.TcpPortDescr)] public int TcpPort { get; set; }
		[ArgDescription(Opts.HttpPortDescr)] public int HttpPort { get; set; }
		public int Timeout { get; set; }
		public int ReadWindow { get; set; }
		public int WriteWindow { get; set; }
		public int PingWindow { get; set; }
		public string[] Command { get; set; }
		public bool Reconnect { get; set; }

		public bool UseTls { get; set; }
		public bool TlsValidateServer { get; set; }

		//[ArgDescription("A connection string to connect to a node/cluster. Used by gRPC only.")]
		public string ConnectionString { get; set; }

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
			ConnectionString = string.Empty;
		}
	}
}
