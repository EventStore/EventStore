// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
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

        public void Parse(params string[] args)
        {
            Command = _helper.Parse(args);
        }

        public string DumpOptions()
        {
            return _helper.DumpOptions();
        }
    }
}
