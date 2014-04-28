using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;
using PowerArgs;

namespace EventStore.ClusterNode
{
    public class ClusterNodeOptions : IOptions
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

        [ArgDescription(Opts.InternalIpDescr)]
        public IPAddress InternalIp { get; set; }
        [ArgDescription(Opts.ExternalIpDescr)]
        public IPAddress ExternalIp { get; set; }
        [ArgDescription(Opts.InternalHttpPortDescr)]
        public int InternalHttpPort { get; set; }
        [ArgDescription(Opts.ExternalHttpPortDescr)]
        public int ExternalHttpPort { get; set; }
        [ArgDescription(Opts.InternalTcpPortDescr)]
        public int InternalTcpPort { get; set; }
        [ArgDescription(Opts.InternalSecureTcpPortDescr)]
        public int InternalSecureTcpPort { get; set; }
        [ArgDescription(Opts.ExternalTcpPortDescr)]
        public int ExternalTcpPort { get; set; }
        [ArgDescription(Opts.ExternalSecureTcpPortDescr)]
        public int ExternalSecureTcpPort { get; set; }
        [ArgDescription(Opts.ForceDescr)]
        public bool Force { get; set; }
        [ArgDescription(Opts.ClusterSizeDescr)]
        public int ClusterSize { get; set; }
        [ArgDescription(Opts.NodePriorityDescr)]
        public int NodePriority { get; set; }
        [ArgDescription(Opts.MinFlushDelayMsDescr)]
        public double MinFlushDelayMs { get; set; }

        [ArgDescription(Opts.CommitCountDescr)]
        public int CommitCount { get; set; }
        [ArgDescription(Opts.PrepareCountDescr)]
        public int PrepareCount { get; set; }

        [ArgDescription(Opts.AdminOnExtDescr)]
        public bool AdminOnExt { get; set; }
        [ArgDescription(Opts.StatsOnExtDescr)]
        public bool StatsOnExt { get; set; }
        [ArgDescription(Opts.GossipOnExtDescr)]
        public bool GossipOnExt { get; set; }
        [ArgDescription(Opts.DisableScavengeMergeDescr)]
        public bool DisableScavengeMerging { get; set; }

        [ArgDescription(Opts.DiscoverViaDnsDescr)]
        public bool DiscoverViaDns { get; set; }
        [ArgDescription(Opts.ClusterDnsDescr)]
        public string ClusterDns { get; set; }
        [ArgDescription(Opts.ClusterGossipPortDescr)]
        public int ClusterGossipPort { get; set; }
        [ArgDescription(Opts.GossipSeedDescr)]
        public IPEndPoint[] GossipSeeds { get; set; }

        [ArgDescription(Opts.StatsPeriodDescr)]
        public int StatsPeriodSec { get; set; }
        [ArgDescription(Opts.CachedChunksDescr)]
        public int CachedChunks { get; set; }
        [ArgDescription(Opts.ChunksCacheSizeDescr)]
        public long ChunksCacheSize { get; set; }
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

        [ArgDescription(Opts.UseInternalSslDescr)]
        public bool UseInternalSsl { get; set; }
        [ArgDescription(Opts.SslTargetHostDescr)]
        public string SslTargetHost { get; set; }
        [ArgDescription(Opts.SslValidateServerDescr)]
        public bool SslValidateServer { get; set; }

        [ArgDescription(Opts.AuthenticationTypeDescr)]
        public string AuthenticationType { get; set; }
        [ArgDescription(Opts.AuthenticationConfigFileDescr)]
        public string AuthenticationConfigFile { get; set; }

        [ArgDescription(Opts.PrepareTimeoutMsDescr)]
        public int PrepareTimeoutMs { get; set; }
        [ArgDescription(Opts.CommitTimeoutMsDescr)]
        public int CommitTimeoutMs { get; set; }

        [ArgDescription(Opts.UnsafeDisableFlushToDiskDescr)]
        public bool UnsafeDisableFlushToDisk { get; set; }

        [ArgDescription(Opts.GossipIntervalMsDescr)]
        public int GossipIntervalMs { get; set; }
        [ArgDescription(Opts.GossipAllowedDifferenceMsDescr)]
        public int GossipAllowedDifferenceMs { get; set; }
        [ArgDescription(Opts.GossipTimeoutMsDescr)]
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
