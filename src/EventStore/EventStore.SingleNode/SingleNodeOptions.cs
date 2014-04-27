using System;
using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;

namespace EventStore.SingleNode
{
    public class SingleNodeOptions : IOptions
    {
        public bool ShowHelp { get; protected set; }
        public bool ShowVersion { get; protected set; }
        public string LogsDir { get; protected set; }
        public string Config { get; protected set; }
        public string[] Defines { get; protected set; }

        public IPAddress Ip { get; protected set; }
        public int TcpPort { get; protected set; }
        public int SecureTcpPort { get; protected set; }
        public int HttpPort { get; protected set; }

        public int StatsPeriodSec { get; protected set; }

        public int CachedChunks { get; protected set; }
        public long ChunksCacheSize { get; protected set; }
        public double MinFlushDelayMs { get; protected set; }
        public int MaxMemTableSize { get; protected set; }

        public string DbPath { get; protected set; }
        public bool InMemDb { get; protected set; }
        public bool SkipDbVerify { get; protected set; }
        public RunProjections RunProjections { get; protected set; }
        public int ProjectionThreads { get; protected set; }
        public int WorkerThreads { get; protected set; }

        public bool DisableScavengeMerging { get; protected set; }

        public string[] HttpPrefixes { get; protected set; }
        public bool EnableTrustedAuth { get; protected set; }

        public string CertificateStoreLocation { get; protected set; }
        public string CertificateStoreName { get; protected set; }
        public string CertificateSubjectName { get; protected set; }
        public string CertificateThumbprint { get; protected set; }

        public string CertificateFile { get; protected set; }
        public string CertificatePassword { get; protected set; }

        public int PrepareTimeoutMs { get; protected set; }
        public int CommitTimeoutMs { get; protected set; }

        public bool Force { get; protected set; }

        public bool UnsafeDisableFlushToDisk { get; protected set; }

        public SingleNodeOptions()
        {
            Config = "singlenode-config.json";

            ShowHelp = Opts.ShowHelpDefault;
            ShowVersion = Opts.ShowVersionDefault;
            LogsDir = Opts.LogsDefault;
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
