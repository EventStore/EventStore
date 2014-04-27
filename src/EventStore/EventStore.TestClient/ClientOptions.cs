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
        public bool ShowHelp { get; protected set; }
        public bool ShowVersion { get; protected set; }
        public string LogsDir { get; protected set; }
        public string[] Defines { get; protected set; }
        public string Config { get; protected set; }

        public IPAddress Ip { get; protected set; }
        public int TcpPort { get; protected set; }
        public int HttpPort { get; protected set; }
        public int Timeout { get; protected set; }
        public int ReadWindow { get; protected set; }
        public int WriteWindow { get; protected set; }
        public int PingWindow { get; protected set; }
        public bool Force { get; protected set; }
        public string[] Command { get; protected set; }

        public ClientOptions()
        {
            Config = "testclient-config.json";

            ShowHelp = Opts.ShowHelpDefault;
            ShowVersion = Opts.ShowVersionDefault;
            LogsDir = Opts.LogsDefault;
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
