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

namespace EventStore.SingleNode
{
    public class SingleNodeOptions : IOptions
    {
        public bool ShowHelp { get { return _helper.Get(() => ShowHelp); } }
        public bool ShowVersion { get { return _helper.Get(() => ShowVersion); } }
        public string LogsDir { get { return _helper.Get(() => LogsDir); } }
        public string[] Configs { get { return _helper.Get(() => Configs); } }
        public string[] Defines { get { return _helper.Get(() => Defines); } }

        public IPAddress Ip { get { return _helper.Get(() => Ip); } }
        public int TcpPort { get { return _helper.Get(() => TcpPort); } }
        public int SecureTcpPort { get { return _helper.Get(() => SecureTcpPort); } }
        public int HttpPort { get { return _helper.Get(() => HttpPort); } }

        public int StatsPeriodSec { get { return _helper.Get(() => StatsPeriodSec); } }
        
        public int CachedChunks { get { return _helper.Get(() => CachedChunks); } }
        public long ChunksCacheSize { get { return _helper.Get(() => ChunksCacheSize); } }
        public double MinFlushDelayMs { get { return _helper.Get(() => MinFlushDelayMs); } }

        public string DbPath { get { return _helper.Get(() => DbPath); } }
        public bool InMemDb { get { return _helper.Get(() => InMemDb); } }
        public bool SkipDbVerify { get { return _helper.Get(() => SkipDbVerify); } }
        public RunProjections RunProjections { get { return _helper.Get(() => RunProjections); } }
        public int ProjectionThreads { get { return _helper.Get(() => ProjectionThreads); } }
        public int WorkerThreads { get { return _helper.Get(() => WorkerThreads); } }

        public bool DisableScavengeMerging { get { return _helper.Get(() => DisableScavengeMerging); } }

        public string[] HttpPrefixes { get { return _helper.Get(() => HttpPrefixes); } }
        public bool EnableTrustedAuth { get { return _helper.Get(() => EnableTrustedAuth); } }

        public string CertificateStore { get { return _helper.Get(() => CertificateStore); } }
        public string CertificateName { get { return _helper.Get(() => CertificateName); } }
        public string CertificateFile { get { return _helper.Get(() => CertificateFile); } }
        public string CertificatePassword { get { return _helper.Get(() => CertificatePassword); } }

        public int PrepareTimeoutMs { get { return _helper.Get(() => PrepareTimeoutMs); } }
        public int CommitTimeoutMs { get { return _helper.Get(() => CommitTimeoutMs); } }

        public bool Force { get { return _helper.Get(() => Force); } }

        private readonly OptsHelper _helper;

        public SingleNodeOptions()
        {
	        _helper = new OptsHelper(() => Configs, Opts.EnvPrefix, "singlenode-config.json");
            
            _helper.Register(() => ShowHelp, Opts.ShowHelpCmd, Opts.ShowHelpEnv, Opts.ShowHelpJson, Opts.ShowHelpDefault, Opts.ShowHelpDescr, false, true);
            _helper.Register(() => ShowVersion, Opts.ShowVersionCmd, Opts.ShowVersionEnv, Opts.ShowVersionJson, Opts.ShowVersionDefault, Opts.ShowVersionDescr, false, true);
            _helper.RegisterRef(() => LogsDir, Opts.LogsCmd, Opts.LogsEnv, Opts.LogsJson, Opts.LogsDefault, Opts.LogsDescr);
            _helper.RegisterArray(() => Configs, Opts.ConfigsCmd, Opts.ConfigsEnv, ",", Opts.ConfigsJson, Opts.ConfigsDefault, Opts.ConfigsDescr);
            _helper.RegisterArray(() => Defines, Opts.DefinesCmd, Opts.DefinesEnv, ",", Opts.DefinesJson, Opts.DefinesDefault, Opts.DefinesDescr, hidden: true);

            _helper.RegisterRef(() => Ip, Opts.IpCmd, Opts.IpEnv, Opts.IpJson, Opts.IpDefault, Opts.IpDescr);
            _helper.Register(() => TcpPort, Opts.TcpPortCmd, Opts.TcpPortEnv, Opts.TcpPortJson, Opts.TcpPortDefault, Opts.TcpPortDescr);
            _helper.Register(() => SecureTcpPort, Opts.SecureTcpPortCmd, Opts.SecureTcpPortEnv, Opts.SecureTcpPortJson, Opts.SecureTcpPortDefault, Opts.SecureTcpPortDescr);
            _helper.Register(() => HttpPort, Opts.HttpPortCmd, Opts.HttpPortEnv, Opts.HttpPortJson, Opts.HttpPortDefault, Opts.HttpPortDescr);
            
            _helper.Register(() => StatsPeriodSec, Opts.StatsPeriodCmd, Opts.StatsPeriodEnv, Opts.StatsPeriodJson, Opts.StatsPeriodDefault, Opts.StatsPeriodDescr);
            
            _helper.Register(() => CachedChunks, Opts.CachedChunksCmd, Opts.CachedChunksEnv, Opts.CachedChunksJson, Opts.CachedChunksDefault, Opts.CachedChunksDescr, hidden: true);
            _helper.Register(() => ChunksCacheSize, Opts.ChunksCacheSizeCmd, Opts.ChunksCacheSizeEnv, Opts.ChunksCacheSizeJson, Opts.ChunksCacheSizeDefault, Opts.ChunksCacheSizeDescr);
            _helper.Register(() => MinFlushDelayMs, Opts.MinFlushDelayMsCmd, Opts.MinFlushDelayMsEnv, Opts.MinFlushDelayMsJson, Opts.MinFlushDelayMsDefault, Opts.MinFlushDelayMsDescr);
            
            _helper.RegisterRef(() => DbPath, Opts.DbPathCmd, Opts.DbPathEnv, Opts.DbPathJson, Opts.DbPathDefault, Opts.DbPathDescr);
            _helper.Register(() => InMemDb, Opts.InMemDbCmd, Opts.InMemDbEnv, Opts.InMemDbJson, Opts.InMemDbDefault, Opts.InMemDbDescr);
            _helper.Register(() => SkipDbVerify, Opts.SkipDbVerifyCmd, Opts.SkipDbVerifyEnv, Opts.SkipDbVerifyJson, Opts.SkipDbVerifyDefault, Opts.SkipDbVerifyDescr);
            _helper.Register(() => RunProjections, Opts.RunProjectionsCmd, Opts.RunProjectionsEnv, Opts.RunProjectionsJson, Opts.RunProjectionsDefault, Opts.RunProjectionsDescr);
            _helper.Register(() => ProjectionThreads, Opts.ProjectionThreadsCmd, Opts.ProjectionThreadsEnv, Opts.ProjectionThreadsJson, Opts.ProjectionThreadsDefault, Opts.ProjectionThreadsDescr);
            _helper.Register(() => WorkerThreads, Opts.WorkerThreadsCmd, Opts.WorkerThreadsEnv, Opts.WorkerThreadsJson, Opts.WorkerThreadsDefault, Opts.WorkerThreadsDescr);

            _helper.RegisterArray(() => HttpPrefixes, Opts.HttpPrefixesCmd, Opts.HttpPrefixesEnv, ",", Opts.HttpPrefixesJson, Opts.HttpPrefixesDefault, Opts.HttpPrefixesDescr);
            _helper.Register(() => EnableTrustedAuth, Opts.EnableTrustedAuthCmd, Opts.EnableTrustedAuthEnv, Opts.EnableTrustedAuthJson, Opts.EnableTrustedAuthDefault, Opts.EnableTrustedAuthDescr);

            _helper.RegisterRef(() => CertificateStore, Opts.CertificateStoreCmd, Opts.CertificateStoreEnv, Opts.CertificateStoreJson, Opts.CertificateStoreDefault, Opts.CertificateStoreDescr);
            _helper.RegisterRef(() => CertificateName, Opts.CertificateNameCmd, Opts.CertificateNameEnv, Opts.CertificateNameJson, Opts.CertificateNameDefault, Opts.CertificateNameDescr);
            _helper.RegisterRef(() => CertificateFile, Opts.CertificateFileCmd, Opts.CertificateFileEnv, Opts.CertificateFileJson, Opts.CertificateFileDefault, Opts.CertificateFileDescr);
            _helper.RegisterRef(() => CertificatePassword, Opts.CertificatePasswordCmd, Opts.CertificatePasswordEnv, Opts.CertificatePasswordJson, Opts.CertificatePasswordDefault, Opts.CertificatePasswordDescr);

            _helper.Register(() => PrepareTimeoutMs, Opts.PrepareTimeoutMsCmd, Opts.PrepareTimeoutMsEnv, Opts.PrepareTimeoutMsJson, Opts.PrepareTimeoutMsDefault, Opts.PrepareTimeoutMsDescr);
            _helper.Register(() => CommitTimeoutMs, Opts.CommitTimeoutMsCmd, Opts.CommitTimeoutMsEnv, Opts.CommitTimeoutMsJson, Opts.CommitTimeoutMsDefault, Opts.CommitTimeoutMsDescr);
            _helper.Register(() => Force, Opts.ForceCmd, Opts.ForceEnv, Opts.ForceJson, false, "Force usage on non-recommended environments such as Boehm GC");
            _helper.Register(() => DisableScavengeMerging, Opts.DisableScavengeMergeCmd, Opts.DisableScavengeMergeEnv, Opts.DisableScavengeMergeJson, Opts.DisableScavengeMergeDefault, Opts.DisableScavengeMergeDescr);
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
