using System.Net;
using EventStore.Common.Options;
using EventStore.Core.Util;

namespace EventStore.SingleNode
{
    [PowerArgs.TabCompletion]
    public class SingleNodeOptions : IOptions
    {
        [ArgDescription(Opts.ShowHelpDescr, Opts.AppGroup)]
        public bool ShowHelp { get; set; }
        [ArgDescription(Opts.ShowVersionDescr, Opts.AppGroup)]
        public bool ShowVersion { get; set; }
        [ArgDescription(Opts.LogsDescr, Opts.AppGroup)]
        public string Log { get; set; }
        [ArgDescription(Opts.ConfigsDescr, Opts.AppGroup)]
        public string Config { get; set; }
        [ArgDescription(Opts.DefinesDescr, Opts.AppGroup)]
        public string[] Defines { get; set; }

        [ArgDescription(Opts.IpDescr, Opts.InterfacesGroup)]
        public IPAddress Ip { get; set; }
        [ArgDescription(Opts.TcpPortDescr, Opts.InterfacesGroup)]
        public int TcpPort { get; set; }
        [ArgDescription(Opts.SecureTcpPortDescr, Opts.InterfacesGroup)]
        public int SecureTcpPort { get; set; }
        [ArgDescription(Opts.HttpPortDescr, Opts.InterfacesGroup)]
        public int HttpPort { get; set; }

        [ArgDescription(Opts.StatsPeriodDescr, Opts.AppGroup)]
        public int StatsPeriodSec { get; set; }

        [ArgDescription(Opts.CachedChunksDescr, Opts.DbGroup)]
        public int CachedChunks { get; set; }
        [ArgDescription(Opts.ChunksCacheSizeDescr, Opts.DbGroup)]
        public long ChunksCacheSize { get; set; }
        [ArgDescription(Opts.MinFlushDelayMsDescr, Opts.DbGroup)]
        public double MinFlushDelayMs { get; set; }
        [ArgDescription(Opts.MaxMemTableSizeDescr, Opts.DbGroup)]
        public int MaxMemTableSize { get; set; }

        [ArgDescription(Opts.DbPathDescr, Opts.DbGroup)]
        public string Db { get; set; }
        [ArgDescription(Opts.InMemDbDescr, Opts.DbGroup)]
        public bool MemDb { get; set; }
        [ArgDescription(Opts.SkipDbVerifyDescr, Opts.DbGroup)]
        public bool SkipDbVerify { get; set; }
        [ArgDescription(Opts.RunProjectionsDescr, Opts.ProjectionsGroup)]
        public ProjectionType RunProjections { get; set; }
        [ArgDescription(Opts.ProjectionThreadsDescr, Opts.ProjectionsGroup)]
        public int ProjectionThreads { get; set; }
        [ArgDescription(Opts.WorkerThreadsDescr, Opts.AppGroup)]
        public int WorkerThreads { get; set; }

        [ArgDescription(Opts.DisableScavengeMergeDescr, Opts.DbGroup)]
        public bool DisableScavengeMerging { get; set; }

        [ArgDescription(Opts.HttpPrefixesDescr, Opts.InterfacesGroup)]
        public string[] HttpPrefixes { get; set; }
        [ArgDescription(Opts.EnableTrustedAuthDescr, Opts.AuthGroup)]
        public bool EnableTrustedAuth { get; set; }

        [ArgDescription(Opts.CertificateStoreLocationDescr, Opts.CertificatesGroup)]
        public string CertificateStoreLocation { get; set; }
        [ArgDescription(Opts.CertificateStoreNameDescr, Opts.CertificatesGroup)]
        public string CertificateStoreName { get; set; }
        [ArgDescription(Opts.CertificateSubjectNameDescr, Opts.CertificatesGroup)]
        public string CertificateSubjectName { get; set; }
        [ArgDescription(Opts.CertificateThumbprintDescr, Opts.CertificatesGroup)]
        public string CertificateThumbprint { get; set; }

        [ArgDescription(Opts.CertificateFileDescr, Opts.CertificatesGroup)]
        public string CertificateFile { get; set; }
        [ArgDescription(Opts.CertificatePasswordDescr, Opts.CertificatesGroup)]
        public string CertificatePassword { get; set; }

        [ArgDescription(Opts.PrepareTimeoutMsDescr, Opts.DbGroup)]
        public int PrepareTimeoutMs { get; set; }
        [ArgDescription(Opts.CommitTimeoutMsDescr, Opts.DbGroup)]
        public int CommitTimeoutMs { get; set; }

        [ArgDescription(Opts.ForceDescr, Opts.AppGroup)]
        public bool Force { get; set; }

        [ArgDescription(Opts.UnsafeDisableFlushToDiskDescr, Opts.DbGroup)]
        public bool UnsafeDisableFlushToDisk { get; set; }

        public SingleNodeOptions()
        {
            Config = "singlenode-config.json";

            ShowHelp = Opts.ShowHelpDefault;
            ShowVersion = Opts.ShowVersionDefault;
            Log = Opts.LogsDefault;
            Defines = Opts.DefinesDefault;

            Ip = Opts.IpDefault;
            TcpPort = Opts.TcpPortDefault;
            SecureTcpPort = Opts.SecureTcpPortDefault;
            HttpPort = Opts.HttpPortDefault;

            StatsPeriodSec = Opts.StatsPeriodDefault;

            CachedChunks = Opts.CachedChunksDefault;
            ChunksCacheSize = Opts.ChunksCacheSizeDefault;
            MinFlushDelayMs = Opts.MinFlushDelayMsDefault;

            Db = Opts.DbPathDefault;
            MemDb = Opts.InMemDbDefault;
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
