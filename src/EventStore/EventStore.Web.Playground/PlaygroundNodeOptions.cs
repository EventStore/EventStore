using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;

namespace EventStore.Web.Playground
{
    public class PlaygroundNodeOptions : IOptions
    {
        public bool ShowHelp { get { return _helper.Get(() => ShowHelp); } }
        public bool ShowVersion { get { return _helper.Get(() => ShowVersion); } }
        public string LogsDir { get { return _helper.Get(() => LogsDir); } }
        public string[] Configs { get { return _helper.Get(() => Configs); } }
        public string[] Defines { get { return _helper.Get(() => Defines); } }
        public IPAddress Ip { get { return _helper.Get(() => Ip); } }
        public int TcpPort { get { return _helper.Get(() => TcpPort); } }
        public int HttpPort { get { return _helper.Get(() => HttpPort); } }
        public string DbPath { get { return _helper.Get(() => DbPath); } }
        public int WorkerThreads { get { return _helper.Get(() => WorkerThreads); } }
        public string[] HttpPrefixes { get { return _helper.Get(() => HttpPrefixes); } }
        public bool Force { get { return false; } }
        private readonly OptsHelper _helper;

        public PlaygroundNodeOptions()
        {
            _helper = new OptsHelper(() => Configs, Opts.EnvPrefix, "config.json");

            _helper.Register(() => ShowHelp, Opts.ShowHelpCmd, Opts.ShowHelpEnv, Opts.ShowHelpJson, Opts.ShowHelpDefault, Opts.ShowHelpDescr);
            _helper.Register(() => ShowVersion, Opts.ShowVersionCmd, Opts.ShowVersionEnv, Opts.ShowVersionJson, Opts.ShowVersionDefault, Opts.ShowVersionDescr);
            _helper.RegisterRef(() => LogsDir, Opts.LogsCmd, Opts.LogsEnv, Opts.LogsJson, Opts.LogsDefault, Opts.LogsDescr);
            _helper.RegisterArray(() => Configs, Opts.ConfigsCmd, Opts.ConfigsEnv, ",", Opts.ConfigsJson, Opts.ConfigsDefault, Opts.ConfigsDescr);
            _helper.RegisterArray(() => Defines, Opts.DefinesCmd, Opts.DefinesEnv, ",", Opts.DefinesJson, Opts.DefinesDefault, Opts.DefinesDescr, hidden: true);

            _helper.RegisterRef(() => Ip, "i|ip=", "IP", "ip", IPAddress.Loopback, "The IP address to bind to.");
            _helper.Register(() => TcpPort, "t|tcp-port=", "TCP_PORT", "tcpPort", 1113, "The port to run the TCP server on.");
            _helper.Register(() => HttpPort, "h|http-port=", "HTTP_PORT", "httpPort", 2113, "The port to run the HTTP server on.");


            _helper.RegisterRef(() => DbPath, Opts.DbPathCmd, Opts.DbPathEnv, Opts.DbPathJson, Opts.DbPathDefault, Opts.DbPathDescr);
            _helper.Register(() => WorkerThreads, Opts.WorkerThreadsCmd, Opts.WorkerThreadsEnv, Opts.WorkerThreadsJson, Opts.WorkerThreadsDefault, Opts.WorkerThreadsDescr);
            _helper.RegisterArray(() => HttpPrefixes, Opts.HttpPrefixesCmd, Opts.HttpPrefixesEnv, ",", Opts.HttpPrefixesJson, Opts.HttpPrefixesDefault, Opts.HttpPrefixesDescr);
        }

        public bool Parse(params string[] args)
        {
            var result = _helper.Parse(args);
            return result.IsEmpty();
        }

        public string DumpOptions()
        {
            return _helper.DumpOptions();
        }

        public string GetUsage()
        {
            return _helper.GetUsage();
        }
    }
}
