using System;
using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;

namespace EventStore.SingleNode
{
    public class SingleNodeOptions : IOptions
    {
        [ArgDescription(Opts.ShowHelpDescr)]
        public bool ShowHelp { get; set; }
        [ArgDescription(Opts.ShowVersionDescr)]
        public bool ShowVersion { get; set; }
        [ArgDescription(Opts.LogsDescr)]
        public string Logsdir { get; set; }
        [ArgDescription(Opts.ConfigsDescr)]
        public string Config { get; set; }
        [ArgDescription(Opts.DefinesDescr)]
        public string[] Defines { get; set; }

        [ArgDescription(Opts.IpDescr)]
        public IPAddress Ip { get; set; }
        [ArgDescription(Opts.TcpPortDescr)]
        public int TcpPort { get; set; }
        [ArgDescription(Opts.SecureTcpPortDescr)]
        public int SecureTcpPort { get; set; }
        [ArgDescription(Opts.HttpPortDescr)]
        public int HttpPort { get; set; }

        [ArgDescription(Opts.StatsPeriodDescr)]
        public int StatsPeriodSec { get; set; }

        [ArgDescription(Opts.CachedChunksDescr)]
        public int CachedChunks { get; set; }
        [ArgDescription(Opts.ChunksCacheSizeDescr)]
        public long ChunksCacheSize { get; set; }
        [ArgDescription(Opts.MinFlushDelayMsDescr)]
        public double MinFlushDelayMs { get; set; }
        [ArgDescription(Opts.MaxMemTableSizeDescr)]
        public int MaxMemTableSize { get; set; }

        [ArgDescription(Opts.DbPathDescr)]
        public string DbPath { get; set; }
        [ArgDescription(Opts.InMemDbDescr)]
        public bool InMemDb { get; set; }
        [ArgDescription(Opts.SkipDbVerifyDescr)]
        public bool SkipDbVerify { get; set; }
        [ArgDescription(Opts.RunProjectionsDescr)]
        public RunProjections RunProjections { get; set; }
        [ArgDescription(Opts.ProjectionThreadsDescr)]
        public int ProjectionThreads { get; set; }
        [ArgDescription(Opts.WorkerThreadsDescr)]
        public int WorkerThreads { get; set; }

        [ArgDescription(Opts.DisableScavengeMergeDescr)]
        public bool DisableScavengeMerging { get; set; }

        [ArgDescription(Opts.HttpPrefixesDescr)]
        public string[] HttpPrefixes { get; set; }
        [ArgDescription(Opts.EnableTrustedAuthDescr)]
        public bool EnableTrustedAuth { get; set; }

        [ArgDescription(Opts.CertificateStoreLocationDescr)]
        public string CertificateStoreLocation { get; set; }
        [ArgDescription(Opts.CertificateStoreNameDescr)]
        public string CertificateStoreName { get; set; }
        [ArgDescription(Opts.CertificateSubjectNameDescr)]
        public string CertificateSubjectName { get; set; }
        [ArgDescription(Opts.CertificateThumbprintDescr)]
        public string CertificateThumbprint { get; set; }

        [ArgDescription(Opts.CertificateFileDescr)]
        public string CertificateFile { get; set; }
        [ArgDescription(Opts.CertificatePasswordDescr)]
        public string CertificatePassword { get; set; }

        [ArgDescription(Opts.PrepareTimeoutMsDescr)]
        public int PrepareTimeoutMs { get; set; }
        [ArgDescription(Opts.CommitTimeoutMsDescr)]
        public int CommitTimeoutMs { get; set; }

        [ArgDescription(Opts.ForceDescr)]
        public bool Force { get; set; }

        [ArgDescription(Opts.UnsafeDisableFlushToDiskDescr)]
        public bool UnsafeDisableFlushToDisk { get; set; }

        public SingleNodeOptions()
        {
            Config = "singlenode-config.json";

            ShowHelp = Opts.ShowHelpDefault;
            ShowVersion = Opts.ShowVersionDefault;
            Logsdir = Opts.LogsDefault;
            Defines = Opts.DefinesDefault;

            Ip = Opts.IpDefault;
            TcpPort = Opts.TcpPortDefault;
            SecureTcpPort = Opts.SecureTcpPortDefault;
            HttpPort = Opts.HttpPortDefault;

            StatsPeriodSec = Opts.StatsPeriodDefault;

            CachedChunks = Opts.CachedChunksDefault;
            ChunksCacheSize = Opts.ChunksCacheSizeDefault;
            MinFlushDelayMs = Opts.MinFlushDelayMsDefault;

            DbPath = Opts.DbPathDefault;
            InMemDb = Opts.InMemDbDefault;
            SkipDbVerify = Opts.SkipDbVerifyDefault;
            RunProjections = Opts.RunProjectionsDefault;
            ProjectionThreads = Opts.ProjectionThreadsDefault;
            WorkerThreads = Opts.WorkerThreadsDefault;

            MaxMemTableSize = Opts.MaxMemtableSizeDefault;

            HttpPrefixes = Opts.HttpPrefixesDefault;
            EnableTrustedAuth = Opts.EnableTrustedAuthDefault;

            CertificateStoreLocation = Opts.CertificateStoreLocationDefault;
            CertificateStoreName = Opts.CertificateStoreNameDefault;
            CertificateSubjectName = Opts.CertificateSubjectNameDefault;
            CertificateThumbprint = Opts.CertificateThumbprintDefault;

            CertificateFile = Opts.CertificateFileDefault;
            CertificatePassword = Opts.CertificatePasswordDefault;

            UnsafeDisableFlushToDisk = Opts.UnsafeDisableFlushToDiskDefault;
            PrepareTimeoutMs = Opts.PrepareTimeoutMsDefault;
            CommitTimeoutMs = Opts.CommitTimeoutMsDefault;
            Force = false;
            DisableScavengeMerging = Opts.DisableScavengeMergeDefault;
        }
    }
}
