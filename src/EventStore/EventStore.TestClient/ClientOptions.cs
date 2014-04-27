using System.Net;
using EventStore.Common.Options;
using EventStore.Core.Util;

namespace EventStore.TestClient
{
    /// <summary>
    /// Data contract for the command-line options accepted by test client.
    /// This contract is handled by CommandLine project for .NET
    /// </summary>
    public sealed class ClientOptions : IOptions
    {
        public bool ShowHelp { get; set; }
        public bool ShowVersion { get; set; }
        public string Logsdir { get; set; }
        public string[] Defines { get; set; }
        public string Config { get; set; }

        public IPAddress Ip { get; set; }
        public int TcpPort { get; set; }
        public int HttpPort { get; set; }
        public int Timeout { get; set; }
        public int ReadWindow { get; set; }
        public int WriteWindow { get; set; }
        public int PingWindow { get; set; }
        public bool Force { get; set; }
        public string[] Command { get; set; }

        public ClientOptions()
        {
            Config = "testclient-config.json";

            ShowHelp = Opts.ShowHelpDefault;
            ShowVersion = Opts.ShowVersionDefault;
            Logsdir = Opts.LogsDefault;
            Defines = Opts.DefinesDefault;

            Ip = IPAddress.Loopback;
            TcpPort = 1113;
            HttpPort = 2113;
            Timeout = -1;
            ReadWindow = 2000;
            WriteWindow = 2000;
            PingWindow = 2000;
            Force = false;
        }

        public ClientOptions Parse(params string[] args)
        {
            return EventStoreOptions.Parse<ClientOptions>(args);
        }

        public string DumpOptions()
        {
            return System.String.Empty;
        }

        public string GetUsage()
        {
            return "Usage: EventStore.Client -i 127.0.0.1 -t 1113 -h 2113\n\n" + EventStoreOptions.GetUsage<ClientOptions>();
        }
    }
}
