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
using System.Net;
using EventStore.Common.Settings;
using EventStore.Core;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Common.Utils;
using System.Linq;

namespace EventStore.SingleNode
{
    public class Program : ProgramBase<SingleNodeOptions>
    {
        protected SingleVNode Node;
        protected TFChunkDb TfDb;
        
        private SingleVNodeAppSettings _appSets;
        private SingleVNodeSettings _vNodeSets;

        protected override void OnArgsParsed(SingleNodeOptions options)
        {
            var now = DateTime.UtcNow;
            TfDb = GetTfDb(options, now);
            _appSets = GetAppSettings(options);
            _vNodeSets = GetVNodeSettings(options);
        }

        protected override void Create()
        {
            Node = new SingleVNode(TfDb, _vNodeSets, _appSets);
        }

        protected override void Start()
        {
            Node.Start();
        }

        public override void Stop()
        {
            Node.Stop();
        }

        protected override string GetLogsDirectory()
        {
            return TfDb.Config.Path + "-logs" ;
        }

        private static TFChunkDb GetTfDb(SingleNodeOptions options, DateTime timeStamp)
        {
            var db = CreateTfDbConfig(options.DbPath, options.HttpPort, timeStamp, options.ChunksToCache);
            return new TFChunkDb(db);
        }

        private static SingleVNodeAppSettings GetAppSettings(SingleNodeOptions options)
        {
            var app = new SingleVNodeAppSettings(TimeSpan.FromSeconds(options.StatsPeriodSec));
            return app;
        }

        private static SingleVNodeSettings GetVNodeSettings(SingleNodeOptions options)
        {
            var tcp = new IPEndPoint(options.Ip, options.TcpPort);
            var http = new IPEndPoint(options.Ip, options.HttpPort);
            var prefixes = !String.IsNullOrEmpty(options.PrefixesString)
                               ? options.PrefixesString.Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries)
                               : new[] {http.ToHttpUrl()};

            var vnodeSettings = new SingleVNodeSettings(tcp, http, prefixes.Select(p => p.Trim()).ToArray());
            return vnodeSettings;
        }
    }
}