using System.Net;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;

namespace EventStore.ClusterNode
{
    public class ClusterNodeOptions : IOptions
    {
        public bool ShowHelp { get; protected set; }
        public bool ShowVersion { get; protected set; }
        public string LogsDir { get; protected set; }
        public string Config { get; protected set; }
        public string[] Defines { get; protected set; }

        public IPAddress InternalIp { get; protected set; }
        public IPAddress ExternalIp { get; protected set; }
        public int InternalHttpPort { get; protected set; }
        public int ExternalHttpPort { get; protected set; }
        public int InternalTcpPort { get; protected set; }
        public int InternalSecureTcpPort { get; protected set; }
        public int ExternalTcpPort { get; protected set; }
        public int ExternalSecureTcpPort { get; protected set; }
        public bool Force { get; protected set; }
        public int ClusterSize { get; protected set; }
        public int NodePriority { get; protected set; }
        public double MinFlushDelayMs { get; protected set; }

        public int CommitCount { get; protected set; }
        public int PrepareCount { get; protected set; }

        public bool AdminOnExt { get; protected set; }
        public bool StatsOnExt { get; protected set; }
        public bool GossipOnExt { get; protected set; }
        public bool DisableScavengeMerging { get; protected set; }

        public bool DiscoverViaDns { get; protected set; }
        public string ClusterDns { get; protected set; }
        public int ClusterGossipPort { get; protected set; }
        public IPEndPoint[] GossipSeeds { get; protected set; }

        public int StatsPeriodSec { get; protected set; }
        public int CachedChunks { get; protected set; }
        public long ChunksCacheSize { get; protected set; }
        public int MaxMemTableSize { get; protected set; }


        public string DbPath { get; protected set; }
        public bool InMemDb { get; protected set; }
        public bool SkipDbVerify { get; protected set; }
        public RunProjections RunProjections { get; protected set; }
        public int ProjectionThreads { get; protected set; }
        public int WorkerThreads { get; protected set; }

        public string[] HttpPrefixes { get; protected set; }
        public bool EnableTrustedAuth { get; protected set; }

        public string CertificateStoreLocation { get; protected set; }
        public string CertificateStoreName { get; protected set; }
        public string CertificateSubjectName { get; protected set; }
        public string CertificateThumbprint { get; protected set; }

        public string CertificateFile { get; protected set; }
        public string CertificatePassword { get; protected set; }

        public bool UseInternalSsl { get; protected set; }
        public string SslTargetHost { get; protected set; }
        public bool SslValidateServer { get; protected set; }

        public string AuthenticationType { get; protected set; }
        public string AuthenticationConfigFile { get; protected set; }

        public int PrepareTimeoutMs { get; protected set; }
        public int CommitTimeoutMs { get; protected set; }

        public bool UnsafeDisableFlushToDisk { get; protected set; }

        public int GossipIntervalMs { get; protected set; }
        public int GossipAllowedDifferenceMs { get; protected set; }
        public int GossipTimeoutMs { get; protected set; }

        public ClusterNodeOptions()
        {
            Config = "clusternode-config.json";
            ShowHelp = Opts.ShowHelpDefault;
            ShowVersion = Opts.ShowVersionDefault;
            LogsDir = Opts.LogsDefault;
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

        public ClusterNodeOptions Parse(params string[] args)
        {
            return EventStoreOptions.Parse<ClusterNodeOptions>(args);
        }

        public string DumpOptions()
        {
            return System.String.Empty;
        }

        public string GetUsage()
        {
            return EventStoreOptions.GetUsage<ClusterNodeOptions>();
        }
    }
}
