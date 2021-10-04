using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;
using EventStore.Rags;
using Serilog;

namespace EventStore.TestClient {
	/// <summary>
	/// Data contract for the command-line options accepted by test client.
	/// This contract is handled by CommandLine project for .NET
	/// </summary>
	public sealed class ClientOptions : IOptions {
		/// <summary>
		/// Print Help information for EventStore.TestClient.
		/// </summary>
		[ArgDescription(Opts.ShowHelpDescr)] public bool Help { get; set; }

		/// <summary>
		/// Print the EventStore.TestClient version.
		/// </summary>
		[ArgDescription(Opts.ShowVersionDescr)]
		public bool Version { get; set; }

		/// <summary>
		/// Path where to keep log files.
		/// </summary>
		[ArgDescription(Opts.LogsDescr)] public string Log { get; set; }

		/// <summary>
		/// The name of the log config file.
		/// </summary>	
		[ArgDescription(Opts.LogConfigDescr)] public string LogConfig { get; set; }

		/// <summary>
		/// Which format (plain, json) to use when writing to the console.
		/// </summary>
		[ArgDescription(Opts.LogConsoleFormatDescr, Opts.AppGroup)]
		public LogConsoleFormat LogConsoleFormat { get; set; }

		/// <summary>
		/// Maximum size of each log file.
		/// </summary>
		[ArgDescription(Opts.LogFileSizeDescr, Opts.AppGroup)] public int LogFileSize { get; set; }

		/// <summary>
		/// How often to rotate logs.
		/// </summary>
		[ArgDescription(Opts.LogFileIntervalDescr, Opts.AppGroup)] public RollingInterval LogFileInterval { get; set; }

		/// <summary>
		/// The number of log files to hold on to.
		/// </summary>
		[ArgDescription(Opts.LogFileRetentionCountDescr, Opts.AppGroup)] public int LogFileRetentionCount { get; set; }

		/// <summary>
		/// Disable log to disk.
		/// </summary>
		[ArgDescription(Opts.DisableLogFileDescr, Opts.AppGroup)] public bool DisableLogFile { get; set; }

		/// <summary>
		/// Path to the config file.
		/// </summary>
		[ArgDescription(Opts.ConfigsDescr)] public string Config { get; set; }

		/// <summary>
		/// Print out the effective configuration without running a command.
		/// </summary>
		[ArgDescription(Opts.WhatIfDescr, Opts.AppGroup)] public bool WhatIf { get; set; }

		/// <summary>
		/// The host of the EventStore node.
		/// </summary>
		[ArgDescription(Opts.HostDescr)] public string Host { get; set; }
		/// <summary>
		/// The TCP port of the EventStore node.
		/// </summary>
		[ArgDescription(Opts.TcpPortDescr)] public int TcpPort { get; set; }
		/// <summary>
		/// The HTTP port of the EventStore node.
		/// </summary>
		[ArgDescription(Opts.HttpPortDescr)] public int HttpPort { get; set; }
		/// <summary>
		/// The timeout for operations.
		/// </summary>
		public int Timeout { get; set; }
		/// <summary>
		/// The read window for raw TCP.
		/// </summary>
		public int ReadWindow { get; set; }
		/// <summary>
		/// The write window for raw TCP.
		/// </summary>
		public int WriteWindow { get; set; }
		/// <summary>
		/// The ping window for raw TCP.
		/// </summary>
		public int PingWindow { get; set; }
		/// <summary>
		/// The command to run.
		/// </summary>
		public string[] Command { get; set; }
		/// <summary>
		/// Whether to reconnect on connection drop.
		/// </summary>
		public bool Reconnect { get; set; }

		/// <summary>
		/// Whether to use TLS.
		/// </summary>
		public bool UseTls { get; set; }
		/// <summary>
		/// Whether to validate the server certificates.
		/// </summary>
		public bool TlsValidateServer { get; set; }

		/// <summary>
		/// A connection string to connect to a node/cluster. Used by gRPC only.
		/// </summary>
		public string ConnectionString { get; set; }

		/// <summary>
		/// Construct a new <see cref="ClientOptions"/>
		/// </summary>
		public ClientOptions() {
			Config = "";
			Command = new string[] { };
			Help = Opts.ShowHelpDefault;
			Version = Opts.ShowVersionDefault;
			Log = Locations.DefaultTestClientLogDirectory;
			LogConfig = Opts.LogConfigDefault;
			LogConsoleFormat = Opts.LogConsoleFormatDefault;
			LogFileSize = Opts.LogFileSizeDefault;
			LogFileInterval = Opts.LogFileIntervalDefault;
			LogFileRetentionCount = Opts.LogFileRetentionCountDefault;
			DisableLogFile = Opts.DisableLogFileDefault;
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
