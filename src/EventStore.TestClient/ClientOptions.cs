using System.Net;
using EventStore.Common.Options;
using EventStore.Core.Util;
using EventStore.Rags;

namespace EventStore.TestClient
{
    /// <summary>
    /// Data contract for the command-line options accepted by test client.
    /// This contract is handled by CommandLine project for .NET
    /// </summary>
    public sealed class ClientOptions : IOptions
    {
        [ArgDescription(Opts.ShowHelpDescr)]
        public bool Help { get; set; }
        [ArgDescription(Opts.ShowVersionDescr)]
        public bool Version { get; set; }
        [ArgDescription(Opts.LogsDescr)]
        public string Log { get; set; }
        [ArgDescription(Opts.ConfigsDescr)]
        public string Config { get; set; }
        [ArgDescription(Opts.DefinesDescr)]
        public string[] Defines { get; set; }
        [ArgDescription(Opts.WhatIfDescr, Opts.AppGroup)]
        public bool WhatIf { get; set; }

        [ArgDescription(Opts.IpDescr)]
        public IPAddress Ip { get; set; }
        [ArgDescription(Opts.TcpPortDescr)]
        public int TcpPort { get; set; }
        [ArgDescription(Opts.HttpPortDescr)]
        public int HttpPort { get; set; }
        public int Timeout { get; set; }
        public int ReadWindow { get; set; }
        public int WriteWindow { get; set; }
        public int PingWindow { get; set; }
        [ArgDescription(Opts.ForceDescr)]
        public bool Force { get; set; }
        public string[] Command { get; set; }

        public ClientOptions()
        {
            Config = "";
            Command = new string[] {};
            Help = Opts.ShowHelpDefault;
            Version = Opts.ShowVersionDefault;
            Log = Opts.LogsDefault;
            Defines = Opts.DefinesDefault;
            WhatIf = Opts.WhatIfDefault;
            Ip = IPAddress.Loopback;
            TcpPort = 1113;
            HttpPort = 2113;
            Timeout = -1;
            ReadWindow = 2000;
            WriteWindow = 2000;
            PingWindow = 2000;
            Force = false;
        }
    }
}
