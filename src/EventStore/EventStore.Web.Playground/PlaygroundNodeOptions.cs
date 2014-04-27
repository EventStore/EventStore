using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;

namespace EventStore.Web.Playground
{
    public class PlaygroundNodeOptions : IOptions
    {
        public bool ShowHelp { get; protected set; }
        public bool ShowVersion { get; protected set; }
        public string LogsDir { get; protected set; }
        public string Config { get; protected set; }
        public string[] Defines { get; protected set; }
        public IPAddress Ip { get; protected set; }
        public int TcpPort { get; protected set; }
        public int HttpPort { get; protected set; }
        public string DbPath { get; protected set; }
        public int WorkerThreads { get; protected set; }
        public string[] HttpPrefixes { get; protected set; }
        public bool Force { get; protected set; }

        public PlaygroundNodeOptions()
        {
            Config = "config.json";

            ShowHelp = Opts.ShowHelpDefault;
            ShowVersion = Opts.ShowVersionDefault;
            LogsDir = Opts.LogsDefault;
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
