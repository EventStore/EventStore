using System;
using System.IO;
using System.Linq;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core;

namespace EventStore.Web.Playground
{
    public class Program : ProgramBase<PlaygroundNodeOptions>
    {
        private PlaygroundVNode _node;
        private readonly DateTime _startupTimeStamp = DateTime.UtcNow;

        public static int Main(string[] args)
        {
            var p = new Program();
            return p.Run(args);
        }

        protected override string GetLogsDirectory(PlaygroundNodeOptions options)
        {
            return ResolveDbPath(options.DbPath, options.HttpPort) + "-logs";
        }

        private string ResolveDbPath(string optionsPath, int nodePort)
        {
            if (optionsPath.IsNotEmptyString())
                return optionsPath;

            return Path.Combine(
                Path.GetTempPath(), "EventStore",
                string.Format("{0:yyyy-MM-dd_HH.mm.ss.ffffff}-Node{1}", _startupTimeStamp, nodePort));
        }

        protected override string GetComponentName(PlaygroundNodeOptions options)
        {
            return string.Format("{0}-{1}-single-node", options.Ip, options.HttpPort);
        }

        protected override void Create(PlaygroundNodeOptions options)
        {
            //var dbPath = Path.GetFullPath(ResolveDbPath(options.DbPath, options.HttpPort));
            var vnodeSettings = GetVNodeSettings(options);


            _node = new PlaygroundVNode(vnodeSettings);
        }

        private static PlaygroundVNodeSettings GetVNodeSettings(PlaygroundNodeOptions options)
        {
            var tcpEndPoint = new IPEndPoint(options.Ip, options.TcpPort);
            var httpEndPoint = new IPEndPoint(options.Ip, options.HttpPort);
            var prefixes = options.HttpPrefixes.IsNotEmpty() ? options.HttpPrefixes : new[] {httpEndPoint.ToHttpUrl()};
            var vnodeSettings = new PlaygroundVNodeSettings(tcpEndPoint, httpEndPoint, prefixes.Select(p => p.Trim()).ToArray(), options.WorkerThreads);
            return vnodeSettings;
        }

        protected override void Start()
        {
            _node.Start();
        }

        public override void Stop()
        {
            _node.Stop(exitProcess: true);
        }
    }
}
