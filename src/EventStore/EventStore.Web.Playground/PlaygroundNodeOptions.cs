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
            _helper = new OptsHelper(() => Configs, Opts.EnvPrefix);

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

        public void Parse(params string[] args)
        {
            _helper.Parse(args);
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
