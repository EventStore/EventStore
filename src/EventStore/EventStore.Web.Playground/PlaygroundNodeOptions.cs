using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;

namespace EventStore.Web.Playground
{
    public class PlaygroundNodeOptions : IOptions
    {
        public bool ShowHelp { get; set; }
        public bool ShowVersion { get; set; }
        public string Logsdir { get; set; }
        public string Config { get; set; }
        public string[] Defines { get; set; }
        public IPAddress Ip { get; set; }
        public int TcpPort { get; set; }
        public int HttpPort { get; set; }
        public string DbPath { get; set; }
        public int WorkerThreads { get; set; }
        public string[] HttpPrefixes { get; set; }
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

        public PlaygroundNodeOptions Parse(params string[] args)
        {
            return EventStoreOptions.Parse<PlaygroundNodeOptions>(args);
        }

        public string DumpOptions()
        {
            return System.String.Empty;
        }

        public string GetUsage()
        {
            return EventStoreOptions.GetUsage<PlaygroundNodeOptions>();
        }
    }
}
