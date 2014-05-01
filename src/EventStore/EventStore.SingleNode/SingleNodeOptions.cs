using System.Net;
using EventStore.Common.Options;
using EventStore.Core.Util;
using PowerArgs;

namespace EventStore.SingleNode
{
    [TabCompletion]
    public class SingleNodeOptions : IOptions
    {
        [Common.Options.ArgDescription(Opts.ShowHelpDescr, Opts.AppGroup)]
        public bool ShowHelp { get; set; }
        [Common.Options.ArgDescription(Opts.ShowVersionDescr, Opts.AppGroup)]
        public bool ShowVersion { get; set; }
        [Common.Options.ArgDescription(Opts.LogsDescr, Opts.AppGroup)]
        public string Logsdir { get; set; }
        [Common.Options.ArgDescription(Opts.ConfigsDescr, Opts.AppGroup)]
        public string Config { get; set; }
        [Common.Options.ArgDescription(Opts.DefinesDescr, Opts.AppGroup)]
        public string[] Defines { get; set; }

        [Common.Options.ArgDescription(Opts.IpDescr, Opts.InterfacesGroup)]
        public IPAddress Ip { get; set; }
        [Common.Options.ArgDescription(Opts.TcpPortDescr, Opts.InterfacesGroup)]
        public int TcpPort { get; set; }
        [Common.Options.ArgDescription(Opts.SecureTcpPortDescr, Opts.InterfacesGroup)]
        public int SecureTcpPort { get; set; }
        [Common.Options.ArgDescription(Opts.HttpPortDescr, Opts.InterfacesGroup)]
        public int HttpPort { get; set; }

        [Common.Options.ArgDescription(Opts.StatsPeriodDescr, Opts.AppGroup)]
        public int StatsPeriodSec { get; set; }

        [Common.Options.ArgDescription(Opts.CachedChunksDescr, Opts.DbGroup)]
        public int CachedChunks { get; set; }
        [Common.Options.ArgDescription(Opts.ChunksCacheSizeDescr, Opts.DbGroup)]
        public long ChunksCacheSize { get; set; }
        [Common.Options.ArgDescription(Opts.MinFlushDelayMsDescr, Opts.DbGroup)]
        public double MinFlushDelayMs { get; set; }
        [Common.Options.ArgDescription(Opts.MaxMemTableSizeDescr, Opts.DbGroup)]
        public int MaxMemTableSize { get; set; }

        [Common.Options.ArgDescription(Opts.DbPathDescr, Opts.DbGroup)]
        public string DbPath { get; set; }
        [Common.Options.ArgDescription(Opts.InMemDbDescr, Opts.DbGroup)]
        public bool InMemDb { get; set; }
        [Common.Options.ArgDescription(Opts.SkipDbVerifyDescr, Opts.DbGroup)]
        public bool SkipDbVerify { get; set; }
        [Common.Options.ArgDescription(Opts.RunProjectionsDescr, Opts.ProjectionsGroup)]
        public ProjectionType RunProjections { get; set; }
        [Common.Options.ArgDescription(Opts.ProjectionThreadsDescr, Opts.ProjectionsGroup)]
        public int ProjectionThreads { get; set; }
        [Common.Options.ArgDescription(Opts.WorkerThreadsDescr, Opts.AppGroup)]
        public int WorkerThreads { get; set; }

        [Common.Options.ArgDescription(Opts.DisableScavengeMergeDescr, Opts.DbGroup)]
        public bool DisableScavengeMerging { get; set; }

        [Common.Options.ArgDescription(Opts.HttpPrefixesDescr, Opts.InterfacesGroup)]
        public string[] HttpPrefixes { get; set; }
        [Common.Options.ArgDescription(Opts.EnableTrustedAuthDescr, Opts.AuthGroup)]
        public bool EnableTrustedAuth { get; set; }

        [Common.Options.ArgDescription(Opts.CertificateStoreLocationDescr, Opts.CertificatesGroup)]
        public string CertificateStoreLocation { get; set; }
        [Common.Options.ArgDescription(Opts.CertificateStoreNameDescr, Opts.CertificatesGroup)]
        public string CertificateStoreName { get; set; }
        [Common.Options.ArgDescription(Opts.CertificateSubjectNameDescr, Opts.CertificatesGroup)]
        public string CertificateSubjectName { get; set; }
        [Common.Options.ArgDescription(Opts.CertificateThumbprintDescr, Opts.CertificatesGroup)]
        public string CertificateThumbprint { get; set; }

        [Common.Options.ArgDescription(Opts.CertificateFileDescr, Opts.CertificatesGroup)]
        public string CertificateFile { get; set; }
        [Common.Options.ArgDescription(Opts.CertificatePasswordDescr, Opts.CertificatesGroup)]
        public string CertificatePassword { get; set; }

        [Common.Options.ArgDescription(Opts.PrepareTimeoutMsDescr, Opts.DbGroup)]
        public int PrepareTimeoutMs { get; set; }
        [Common.Options.ArgDescription(Opts.CommitTimeoutMsDescr, Opts.DbGroup)]
        public int CommitTimeoutMs { get; set; }

        [Common.Options.ArgDescription(Opts.ForceDescr, Opts.AppGroup)]
        public bool Force { get; set; }

        [Common.Options.ArgDescription(Opts.UnsafeDisableFlushToDiskDescr, Opts.DbGroup)]
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
