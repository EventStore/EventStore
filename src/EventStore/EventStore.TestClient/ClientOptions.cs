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
	    private const string DefaultJsonConfigFileName = "testclient-config.json";

        public bool ShowHelp { get { return _helper.Get(() => ShowHelp); } }
        public bool ShowVersion { get { return _helper.Get(() => ShowVersion); } }
        public string LogsDir { get { return _helper.Get(() => LogsDir); } }
        public string[] Defines { get { return _helper.Get(() => Defines); } }

        public IPAddress Ip { get { return _helper.Get(() => Ip); } }
        public int TcpPort { get { return _helper.Get(() => TcpPort); } }
        public int HttpPort { get { return _helper.Get(() => HttpPort); } }
        public int Timeout { get { return _helper.Get(() => Timeout); } }
        public int ReadWindow { get { return _helper.Get(() => ReadWindow); } }
        public int WriteWindow { get { return _helper.Get(() => WriteWindow); } }
        public int PingWindow { get { return _helper.Get(() => PingWindow); } }
        public bool Force { get { return _helper.Get(() => Force); } }
        public string[] Command { get; private set; }

        private readonly OptsHelper _helper;

        public ClientOptions()
        {
			_helper = new OptsHelper(null, Opts.EnvPrefix, DefaultJsonConfigFileName);

            _helper.Register(() => ShowHelp, Opts.ShowHelpCmd, Opts.ShowHelpEnv, Opts.ShowHelpJson, Opts.ShowHelpDefault, Opts.ShowHelpDescr);
            _helper.Register(() => ShowVersion, Opts.ShowVersionCmd, Opts.ShowVersionEnv, Opts.ShowVersionJson, Opts.ShowVersionDefault, Opts.ShowVersionDescr);
            _helper.RegisterRef(() => LogsDir, Opts.LogsCmd, Opts.LogsEnv, Opts.LogsJson, Opts.LogsDefault, Opts.LogsDescr);
            _helper.RegisterArray(() => Defines, Opts.DefinesCmd, Opts.DefinesEnv, ",", Opts.DefinesJson, Opts.DefinesDefault, Opts.DefinesDescr, hidden: true);

            _helper.RegisterRef(() => Ip, "i|ip=", null, null, IPAddress.Loopback, "IP address of server.");
            _helper.Register(() => TcpPort, "t|tcp-port=", null, null, 1113, "TCP port on server.");
            _helper.Register(() => HttpPort, "h|http-port=", null, null, 2113, "HTTP port on server.");
            _helper.Register(() => Timeout, "timeout=", null, null, -1, "Timeout for command execution in seconds, -1 for infinity.");
            _helper.Register(() => ReadWindow, "r|read-window=", null, null, 2000, "The difference between sent/received read commands.");
            _helper.Register(() => WriteWindow, "w|write-window=", null, null, 2000, "The difference between sent/received write commands.");
            _helper.Register(() => PingWindow, "p|ping-window=", null, null, 2000, "The difference between sent/received ping commands.");
            _helper.Register(() => Force, "f|force", null, null, false, "Force usage on non-recommended environments such as Boehm GC");
        }

        public string GetUsage()
        {
            return "Usage: EventStore.Client -i 127.0.0.1 -t 1113 -h 2113\n\n" + _helper.GetUsage();
        }

        public bool Parse(params string[] args)
        {
            Command = _helper.Parse(args);
            return true;
        }

        public string DumpOptions()
        {
            return _helper.DumpOptions();
        }
    }
}
