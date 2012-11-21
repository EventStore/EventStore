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
using System;
using System.IO;
using System.Net;
using EventStore.Core;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Common.Utils;
using System.Linq;

namespace EventStore.SingleNode
{
    public class Program : ProgramBase<SingleNodeOptions>
    {
        private SingleVNode _node;
        private Projections.Core.Projections _projections;
        private readonly DateTime _startupTimeStamp = DateTime.UtcNow;

        public static int Main(string[] args)
        {
            var p = new Program();
            return p.Run(args);
        }

        protected override string GetLogsDirectory(SingleNodeOptions options)
        {
            return ResolveDbPath(options.DbPath, options.HttpPort) + "-logs";
        }

        private string ResolveDbPath(string optionsPath, int nodePort)
        {
            if (optionsPath.IsNotEmptyString())
                return optionsPath;

            return Path.Combine(Path.GetTempPath(),
                                "EventStore",
                                string.Format("{0:yyyy-MM-dd_HH.mm.ss.ffffff}-Node{1}", _startupTimeStamp, nodePort));
        }

        protected override string GetComponentName(SingleNodeOptions options)
        {
            return string.Format("{0}-{1}", options.Ip, options.HttpPort);
        }

        protected override void Create(SingleNodeOptions options)
        {
            var dbPath = ResolveDbPath(options.DbPath, options.HttpPort);
            var db = new TFChunkDb(CreateDbConfig(dbPath, options.ChunksToCache));
            var vnodeSettings = GetVNodeSettings(options);
            var appSettings = new SingleVNodeAppSettings(TimeSpan.FromSeconds(options.StatsPeriodSec));
            var dbVerifyHashes = !options.DoNotVerifyDbHashesOnStartup;
            _node = new SingleVNode(db, vnodeSettings, appSettings, dbVerifyHashes);

            if (!options.NoProjections)
            {
                _projections = new Projections.Core.Projections(db,
                                                                _node.MainQueue,
                                                                _node.Bus,
                                                                _node.TimerService,
                                                                _node.HttpService,
                                                                options.ProjectionThreads);
            }
        }

        private static SingleVNodeSettings GetVNodeSettings(SingleNodeOptions options)
        {
            var tcp = new IPEndPoint(options.Ip, options.TcpPort);
            var http = new IPEndPoint(options.Ip, options.HttpPort);
            var prefixes = options.PrefixesString.IsNotEmptyString()
                                   ? options.PrefixesString.Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries)
                                   : new[] {http.ToHttpUrl()};

            var vnodeSettings = new SingleVNodeSettings(tcp, http, prefixes.Select(p => p.Trim()).ToArray());
            return vnodeSettings;
        }

        protected override void Start()
        {
            _node.Start();

            if (_projections != null)
                _projections.Start();
        }

        public override void Stop()
        {
            _node.Stop();
        }
    }
}