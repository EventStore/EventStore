using System;
using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;

namespace EventStore.SingleNode
{
    public class SingleNodeOptions : IOptions
    {
        public bool ShowHelp { get; set; }
        public bool ShowVersion { get; set; }
        public string Logsdir { get; set; }
        public string Config { get; set; }
        public string[] Defines { get; set; }

        public IPAddress Ip { get; set; }
        public int TcpPort { get; set; }
        public int SecureTcpPort { get; set; }
        public int HttpPort { get; set; }

        public int StatsPeriodSec { get; set; }

        public int CachedChunks { get; set; }
        public long ChunksCacheSize { get; set; }
        public double MinFlushDelayMs { get; set; }
        public int MaxMemTableSize { get; set; }

        public string DbPath { get; set; }
        public bool InMemDb { get; set; }
        public bool SkipDbVerify { get; set; }
        public RunProjections RunProjections { get; set; }
        public int ProjectionThreads { get; set; }
        public int WorkerThreads { get; set; }

        public bool DisableScavengeMerging { get; set; }

        public string[] HttpPrefixes { get; set; }
        public bool EnableTrustedAuth { get; set; }

        public string CertificateStoreLocation { get; set; }
        public string CertificateStoreName { get; set; }
        public string CertificateSubjectName { get; set; }
        public string CertificateThumbprint { get; set; }

        public string CertificateFile { get; set; }
        public string CertificatePassword { get; set; }

        public int PrepareTimeoutMs { get; set; }
        public int CommitTimeoutMs { get; set; }

        public bool Force { get; set; }

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

        public SingleNodeOptions Parse(params string[] args)
        {
            return EventStoreOptions.Parse<SingleNodeOptions>(args);
        }

        public string DumpOptions()
        {
            return System.String.Empty;
        }

        public string GetUsage()
        {
            return EventStoreOptions.GetUsage<SingleNodeOptions>();
        }
    }
}
