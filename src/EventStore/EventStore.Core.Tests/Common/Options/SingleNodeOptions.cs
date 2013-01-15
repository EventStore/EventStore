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

namespace EventStore.Core.Tests.Common.Options
{
    public class SingleNodeOptions : IOptions
    {
        public bool ShowHelp { get { return _helper.Get(() => ShowHelp); } }

        public IPAddress Ip { get { return _helper.Get(() => Ip); } }
        public int TcpPort { get { return _helper.Get(() => TcpPort); } }
        public int HttpPort { get { return _helper.Get(() => HttpPort); } }
        public int StatsPeriodSec { get { return _helper.Get(() => StatsPeriodSec); } }
        public int ChunksToCache { get { return _helper.Get(() => ChunksToCache); } }
        public string DbPath { get { return _helper.Get(() => DbPath); } }
        public bool SkipDbVerify { get { return _helper.Get(() => SkipDbVerify); } }
        public bool RunProjections { get { return _helper.Get(() => RunProjections); } }
        public int ProjectionThreads { get { return _helper.Get(() => ProjectionThreads); } }
        public int TcpSendThreads { get { return _helper.Get(() => TcpSendThreads); } }
        public int HttpReceiveThreads { get { return _helper.Get(() => HttpReceiveThreads); } }
        public int HttpSendThreads { get { return _helper.Get(() => HttpSendThreads); } }
        public string[] HttpPrefixes { get { return _helper.Get(() => HttpPrefixes); } }

        public string LogsDir { get { return _helper.Get(() => LogsDir); } }
        public string[] Configs { get { return _helper.Get(() => Configs); } }

        private readonly OptsHelper _helper;

        public SingleNodeOptions()
        {
            _helper = new OptsHelper(() => Configs, "EVENTSTORE_");
            _helper.Register(() => ShowHelp, "h|help", null, null, (bool?)false, "Show help.");

            _helper.RegisterRef(() => Ip, "i|ip=", "net.ip", "ip", IPAddress.Loopback, "The IP address to bind to.");
            _helper.Register(() => TcpPort, "t|tcp-port=", null, null, (int?)1113, "The port to run the TCP server on.");
            _helper.Register(() => HttpPort, "h|http-port=", null, null, (int?)2113, "The port to run the HTTP server on.");
            _helper.Register(() => StatsPeriodSec, "s|stats-period-sec=", null, null, (int?)30, "The number of seconds between statistics gathers.");
            _helper.Register(() => ChunksToCache, "c|chunkcache=", null, null, (int?)2, "The number of chunks to cache in unmanaged memory.");
            _helper.RegisterRef(() => DbPath, "db=", null, null, string.Empty, "The path the db should be loaded/saved to.");
            _helper.Register(() => SkipDbVerify, "skip-db-verify|do-not-verify-db-hashes-on-startup", null, null, (bool?)false, "Bypasses the checking of file hashes of database during startup (allows for faster startup).");
            _helper.Register(() => RunProjections, "run-projections", null, null, (bool?)false, "Enables the running of JavaScript projections (experimental).");
            _helper.Register(() => ProjectionThreads, "projection-threads=", null, null, (int?)3, "The number of threads to use for projections.");
            _helper.Register(() => TcpSendThreads, "tcp-send-threads=", null, null, (int?)3, "The number of threads to use for sending to TCP sockets.");
            _helper.Register(() => HttpReceiveThreads, "http-receive-threads=", null, null, (int?)5, "The number of threads to use for receiving from HTTP.");
            _helper.Register(() => HttpSendThreads, "http-send-threads=", null, null, (int?)3, "The number of threads for sending over HTTP.");
            _helper.RegisterArray(() => HttpPrefixes, "prefixes|http-prefix=", null, null, null, new string[0], "The prefixes that the http server should respond to.");

            _helper.RegisterRef(() => LogsDir, "logsdir|log=", null, "LOGSDIR", string.Empty, "Path where to keep log files.");
            _helper.RegisterArray(() => Configs, "cfg|config=", null, null, null, new string[0], "Configuration files.");

            _helper.Parse();
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