using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;

namespace EventStore.Web.Playground
{
    public class PlaygroundNodeOptions : IOptions
    {
        [ArgDescription(Opts.ShowHelpDescr)]
        public bool ShowHelp { get; set; }
        [ArgDescription(Opts.ShowVersionDescr)]
        public bool ShowVersion { get; set; }
        [ArgDescription(Opts.LogsDescr)]
        public string Logsdir { get; set; }
        [ArgDescription(Opts.ConfigsDescr)]
        public string Config { get; set; }
        [ArgDescription(Opts.DefinesDescr)]
        public string[] Defines { get; set; }

        [ArgDescription(Opts.IpDescr)]
        public IPAddress Ip { get; set; }
        [ArgDescription(Opts.TcpPortDescr)]
        public int TcpPort { get; set; }
        [ArgDescription(Opts.HttpPortDescr)]
        public int HttpPort { get; set; }
        [ArgDescription(Opts.DbPathDescr)]
        public string DbPath { get; set; }
        [ArgDescription(Opts.WorkerThreadsDescr)]
        public int WorkerThreads { get; set; }
        [ArgDescription(Opts.HttpPrefixesDescr)]
        public string[] HttpPrefixes { get; set; }
        [ArgDescription(Opts.ForceDescr)]
        public bool Force { get; set; }

        public PlaygroundNodeOptions()
        {
            Config = "config.json";

            ShowHelp = Opts.ShowHelpDefault;
            ShowVersion = Opts.ShowVersionDefault;
            Logsdir = Opts.LogsDefault;
            Defines = Opts.DefinesDefault;

            Ip = IPAddress.Loopback;
            TcpPort = 1113;
            HttpPort = 2113;

            DbPath = Opts.DbPathDefault;
            WorkerThreads = Opts.WorkerThreadsDefault;
            HttpPrefixes = Opts.HttpPrefixesDefault;
        }
    }
}
