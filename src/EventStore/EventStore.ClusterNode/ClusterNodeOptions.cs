using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;

namespace EventStore.ClusterNode
{
    public class ClusterNodeOptions : IOptions
    {
        public bool ShowHelp { get; set; }
        public bool ShowVersion { get; set; }
        public string Logsdir { get; set; }
        public string Config { get; set; }
        public string[] Defines { get; set; }

        public IPAddress InternalIp { get; set; }
        public IPAddress ExternalIp { get; set; }
        public int InternalHttpPort { get; set; }
        public int ExternalHttpPort { get; set; }
        public int InternalTcpPort { get; set; }
        public int InternalSecureTcpPort { get; set; }
        public int ExternalTcpPort { get; set; }
        public int ExternalSecureTcpPort { get; set; }
        public bool Force { get; set; }
        public int ClusterSize { get; set; }
        public int NodePriority { get; set; }
        public double MinFlushDelayMs { get; set; }

        public int CommitCount { get; set; }
        public int PrepareCount { get; set; }

        public bool AdminOnExt { get; set; }
        public bool StatsOnExt { get; set; }
        public bool GossipOnExt { get; set; }
        public bool DisableScavengeMerging { get; set; }

        public bool DiscoverViaDns { get; set; }
        public string ClusterDns { get; set; }
        public int ClusterGossipPort { get; set; }
        public IPEndPoint[] GossipSeeds { get; set; }

        public int StatsPeriodSec { get; set; }
        public int CachedChunks { get; set; }
        public long ChunksCacheSize { get; set; }
        public int MaxMemTableSize { get; set; }


        public string DbPath { get; set; }
        public bool InMemDb { get; set; }
        public bool SkipDbVerify { get; set; }
        public RunProjections RunProjections { get; set; }
        public int ProjectionThreads { get; set; }
        public int WorkerThreads { get; set; }

        public string[] HttpPrefixes { get; set; }
        public bool EnableTrustedAuth { get; set; }

        public string CertificateStoreLocation { get; set; }
        public string CertificateStoreName { get; set; }
        public string CertificateSubjectName { get; set; }
        public string CertificateThumbprint { get; set; }

        public string CertificateFile { get; set; }
        public string CertificatePassword { get; set; }

        public bool UseInternalSsl { get; set; }
        public string SslTargetHost { get; set; }
        public bool SslValidateServer { get; set; }

        public string AuthenticationType { get; set; }
        public string AuthenticationConfigFile { get; set; }

        public int PrepareTimeoutMs { get; set; }
        public int CommitTimeoutMs { get; set; }

        public bool UnsafeDisableFlushToDisk { get; set; }

        public int GossipIntervalMs { get; set; }
        public int GossipAllowedDifferenceMs { get; set; }
        public int GossipTimeoutMs { get; set; }

        public ClusterNodeOptions()
        {
            Config = "clusternode-config.json";
            ShowHelp = Opts.ShowHelpDefault;
            ShowVersion = Opts.ShowVersionDefault;
            Logsdir = Opts.LogsDefault;
            Defines = Opts.DefinesDefault;

            InternalIp = Opts.InternalIpDefault;
            ExternalIp = Opts.ExternalIpDefault;
            InternalHttpPort = Opts.InternalHttpPortDefault;
            ExternalHttpPort = Opts.ExternalHttpPortDefault;
            InternalTcpPort = Opts.InternalTcpPortDefault;
            InternalSecureTcpPort = Opts.InternalSecureTcpPortDefault;
            ExternalTcpPort = Opts.ExternalTcpPortDefault;
            ExternalSecureTcpPort = Opts.ExternalSecureTcpPortDefault;
            Force = Opts.ForceDefault;
            ClusterSize = Opts.ClusterSizeDefault;
            MinFlushDelayMs = Opts.MinFlushDelayMsDefault;
            NodePriority = Opts.NodePriorityDefault;

            CommitCount = Opts.CommitCountDefault;
            PrepareCount = Opts.PrepareCountDefault;
            MaxMemTableSize = Opts.MaxMemtableSizeDefault;

            DiscoverViaDns = Opts.DiscoverViaDnsDefault;
            ClusterDns = Opts.ClusterDnsDefault;
            ClusterGossipPort = Opts.ClusterGossipPortDefault;
            GossipSeeds = Opts.GossipSeedDefault;

            StatsPeriodSec = Opts.StatsPeriodDefault;

            CachedChunks = Opts.CachedChunksDefault;
            ChunksCacheSize = Opts.ChunksCacheSizeDefault;

            DbPath = Opts.DbPathDefault;
            InMemDb = Opts.InMemDbDefault;
            SkipDbVerify = Opts.SkipDbVerifyDefault;
            RunProjections = Opts.RunProjectionsDefault;
            ProjectionThreads = Opts.ProjectionThreadsDefault;
            WorkerThreads = Opts.WorkerThreadsDefault;

            HttpPrefixes = Opts.HttpPrefixesDefault;
            EnableTrustedAuth = Opts.EnableTrustedAuthDefault;

            CertificateStoreLocation = Opts.CertificateStoreLocationDefault;
            CertificateStoreName = Opts.CertificateStoreNameDefault;
            CertificateSubjectName = Opts.CertificateSubjectNameDefault;
            CertificateThumbprint = Opts.CertificateThumbprintDefault;

            CertificateFile = Opts.CertificateFileDefault;
            CertificatePassword = Opts.CertificatePasswordDefault;

            UseInternalSsl = Opts.UseInternalSslDefault;
            SslTargetHost = Opts.SslTargetHostDefault;
            SslValidateServer = Opts.SslValidateServerDefault;

            AuthenticationType = Opts.AuthenticationTypeDefault;
            AuthenticationConfigFile = Opts.AuthenticationConfigFileDefault;

            UnsafeDisableFlushToDisk = Opts.UnsafeDisableFlushToDiskDefault;
            PrepareTimeoutMs = Opts.PrepareTimeoutMsDefault;
            CommitTimeoutMs = Opts.CommitTimeoutMsDefault;
            DisableScavengeMerging = Opts.DisableScavengeMergeDefault;
            GossipOnExt = Opts.GossipOnExtDefault;
            StatsOnExt = Opts.StatsOnExtDefault;
            AdminOnExt = Opts.AdminOnExtDefault;
            GossipIntervalMs = Opts.GossipIntervalMsDefault;
            GossipAllowedDifferenceMs = Opts.GossipAllowedDifferenceMsDefault;
            GossipTimeoutMs = Opts.GossipTimeoutMsDefault;
        }
    }
}
