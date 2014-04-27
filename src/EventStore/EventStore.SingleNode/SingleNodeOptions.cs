using System;
using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
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
        public int MaxMemTableSize { get { return _helper.Get(() => MaxMemTableSize); } }

        public string DbPath { get { return _helper.Get(() => DbPath); } }
        public bool InMemDb { get { return _helper.Get(() => InMemDb); } }
        public bool SkipDbVerify { get { return _helper.Get(() => SkipDbVerify); } }
        public RunProjections RunProjections { get { return _helper.Get(() => RunProjections); } }
        public int ProjectionThreads { get { return _helper.Get(() => ProjectionThreads); } }
        public int WorkerThreads { get { return _helper.Get(() => WorkerThreads); } }

        public bool DisableScavengeMerging { get { return _helper.Get(() => DisableScavengeMerging); } }

        public string[] HttpPrefixes { get { return _helper.Get(() => HttpPrefixes); } }
        public bool EnableTrustedAuth { get { return _helper.Get(() => EnableTrustedAuth); } }

        public string CertificateStoreLocation { get { return _helper.Get(() => CertificateStoreLocation); } }
        public string CertificateStoreName { get { return _helper.Get(() => CertificateStoreName); } }
        public string CertificateSubjectName { get { return _helper.Get(() => CertificateSubjectName); } }
        public string CertificateThumbprint { get { return _helper.Get(() => CertificateThumbprint); } }

        public string CertificateFile { get { return _helper.Get(() => CertificateFile); } }
        public string CertificatePassword { get { return _helper.Get(() => CertificatePassword); } }
        
        public int PrepareTimeoutMs { get { return _helper.Get(() => PrepareTimeoutMs); } }
        public int CommitTimeoutMs { get { return _helper.Get(() => CommitTimeoutMs); } }

        public bool Force { get { return _helper.Get(() => Force); } }

        public bool UnsafeDisableFlushToDisk { get { return _helper.Get(() => UnsafeDisableFlushToDisk); } }

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

            _helper.Register(() => MaxMemTableSize, Opts.MaxMemTableSizeCmd, Opts.MaxMemTableSizeEnv, Opts.MaxMemTableSizeJson, Opts.MaxMemtableSizeDefault, Opts.MaxMemTableSizeDescr);

            _helper.RegisterArray(() => HttpPrefixes, Opts.HttpPrefixesCmd, Opts.HttpPrefixesEnv, ",", Opts.HttpPrefixesJson, Opts.HttpPrefixesDefault, Opts.HttpPrefixesDescr);
            _helper.Register(() => EnableTrustedAuth, Opts.EnableTrustedAuthCmd, Opts.EnableTrustedAuthEnv, Opts.EnableTrustedAuthJson, Opts.EnableTrustedAuthDefault, Opts.EnableTrustedAuthDescr);

            _helper.RegisterRef(() => CertificateStoreLocation, Opts.CertificateStoreLocationCmd, Opts.CertificateStoreLocationEnv, Opts.CertificateStoreLocationJson, Opts.CertificateStoreLocationDefault, Opts.CertificateStoreLocationDescr);
            _helper.RegisterRef(() => CertificateStoreName, Opts.CertificateStoreNameCmd, Opts.CertificateStoreNameEnv, Opts.CertificateStoreNameJson, Opts.CertificateStoreNameDefault, Opts.CertificateStoreNameDescr);
            _helper.RegisterRef(() => CertificateSubjectName, Opts.CertificateSubjectNameCmd, Opts.CertificateSubjectNameEnv, Opts.CertificateSubjectNameJson, Opts.CertificateSubjectNameDefault, Opts.CertificateSubjectNameDescr);
            _helper.RegisterRef(() => CertificateThumbprint, Opts.CertificateThumbprintCmd, Opts.CertificateThumbprintEnv, Opts.CertificateThumbprintJson, Opts.CertificateThumbprintDefault, Opts.CertificateThumbprintDescr);

            _helper.RegisterRef(() => CertificateFile, Opts.CertificateFileCmd, Opts.CertificateFileEnv, Opts.CertificateFileJson, Opts.CertificateFileDefault, Opts.CertificateFileDescr);
            _helper.RegisterRef(() => CertificatePassword, Opts.CertificatePasswordCmd, Opts.CertificatePasswordEnv, Opts.CertificatePasswordJson, Opts.CertificatePasswordDefault, Opts.CertificatePasswordDescr);

            _helper.Register(() => UnsafeDisableFlushToDisk, Opts.UnsafeDisableFlushToDiskCmd, Opts.UnsafeDisableFlushToDiskEnv, Opts.PrepareTimeoutMsJson, Opts.UnsafeDisableFlushToDiskDefault, Opts.UnsafeDisableFlushToDiskDescr);
            _helper.Register(() => PrepareTimeoutMs, Opts.PrepareTimeoutMsCmd, Opts.PrepareTimeoutMsEnv, Opts.PrepareTimeoutMsJson, Opts.PrepareTimeoutMsDefault, Opts.PrepareTimeoutMsDescr);
            _helper.Register(() => CommitTimeoutMs, Opts.CommitTimeoutMsCmd, Opts.CommitTimeoutMsEnv, Opts.CommitTimeoutMsJson, Opts.CommitTimeoutMsDefault, Opts.CommitTimeoutMsDescr);
            _helper.Register(() => Force, Opts.ForceCmd, Opts.ForceEnv, Opts.ForceJson, false, "Force usage on non-recommended environments such as Boehm GC");
            _helper.Register(() => DisableScavengeMerging, Opts.DisableScavengeMergeCmd, Opts.DisableScavengeMergeEnv, Opts.DisableScavengeMergeJson, Opts.DisableScavengeMergeDefault, Opts.DisableScavengeMergeDescr);
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
