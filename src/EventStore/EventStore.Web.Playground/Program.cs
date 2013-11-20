﻿// Copyright (c) 2012, Event Store LLP
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
