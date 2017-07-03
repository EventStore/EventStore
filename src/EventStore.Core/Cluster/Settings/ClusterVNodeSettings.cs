using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;
using EventStore.Core.Authentication;
using EventStore.Core.Data;
using EventStore.Core.Services.Monitoring;

namespace EventStore.Core.Cluster.Settings
{
    public class ClusterVNodeSettings
    {
        public readonly VNodeInfo NodeInfo;
        public readonly string[] HttpPrefixes;
        public readonly bool EnableTrustedAuth;
        public readonly X509Certificate2 Certificate;
        public readonly int WorkerThreads;
        public readonly bool DevelopmentMode;

        public readonly bool DiscoverViaDns;
        public readonly string ClusterDns;
        public readonly IPEndPoint[] GossipSeeds;
        public readonly bool EnableHistograms;
        public readonly TimeSpan MinFlushDelay;

        public readonly int ClusterNodeCount;
        public readonly int PrepareAckCount;
        public readonly int CommitAckCount;
        public readonly TimeSpan PrepareTimeout;
        public readonly TimeSpan CommitTimeout;

        public readonly int NodePriority;

        public readonly bool UseSsl;
        public readonly string SslTargetHost;
        public readonly bool SslValidateServer;

        public readonly TimeSpan StatsPeriod;
        public readonly StatsStorage StatsStorage;

        public readonly IAuthenticationProviderFactory AuthenticationProviderFactory;
        public readonly bool DisableScavengeMerging;
        public bool AdminOnPublic;
        public bool StatsOnPublic;
        public bool GossipOnPublic;
        public readonly TimeSpan GossipInterval;
        public readonly TimeSpan GossipAllowedTimeDifference;
        public readonly TimeSpan GossipTimeout;
        public readonly TimeSpan IntTcpHeartbeatTimeout;
        public readonly TimeSpan IntTcpHeartbeatInterval;
        public readonly TimeSpan ExtTcpHeartbeatTimeout;
        public readonly TimeSpan ExtTcpHeartbeatInterval;

        public readonly bool VerifyDbHash;
        public readonly int MaxMemtableEntryCount;
        public readonly int IndexCacheDepth;

        public ClusterVNodeSettings(Guid instanceId, int debugIndex,
                                    IPEndPoint internalTcpEndPoint,
                                    IPEndPoint internalSecureTcpEndPoint,
                                    IPEndPoint externalTcpEndPoint,
                                    IPEndPoint externalSecureTcpEndPoint,
                                    IPEndPoint internalHttpEndPoint,
                                    IPEndPoint externalHttpEndPoint,
                                    string[] httpPrefixes,
                                    bool enableTrustedAuth,
                                    X509Certificate2 certificate,
                                    int workerThreads,
                                    bool discoverViaDns,
                                    string clusterDns,
                                    IPEndPoint[] gossipSeeds,
                                    TimeSpan minFlushDelay,
                                    int clusterNodeCount,
                                    int prepareAckCount,
                                    int commitAckCount,
                                    TimeSpan prepareTimeout,
                                    TimeSpan commitTimeout,
                                    bool useSsl,
                                    string sslTargetHost,
                                    bool sslValidateServer,
                                    TimeSpan statsPeriod,
                                    StatsStorage statsStorage,
                                    int nodePriority,
                                    IAuthenticationProviderFactory authenticationProviderFactory,
                                    bool disableScavengeMerging,
                                    bool adminOnPublic,
                                    bool statsOnPublic,
                                    bool gossipOnPublic,
                                    TimeSpan gossipInterval,
                                    TimeSpan gossipAllowedTimeDifference,
                                    TimeSpan gossipTimeout,
                                    TimeSpan intTcpHeartbeatTimeout,
                                    TimeSpan intTcpHeartbeatInterval,
                                    TimeSpan extTcpHeartbeatTimeout,
                                    TimeSpan extTcpHeartbeatInterval,
				                    bool verifyDbHash,
				                    int maxMemtableEntryCount,
                                    bool developmentMode,
                                    bool enableHistograms = false,
                                    int indexCacheDepth = 12)
        {
            Ensure.NotEmptyGuid(instanceId, "instanceId");
            Ensure.NotNull(internalTcpEndPoint, "internalTcpEndPoint");
            Ensure.NotNull(externalTcpEndPoint, "externalTcpEndPoint");
            Ensure.NotNull(internalHttpEndPoint, "internalHttpEndPoint");
            Ensure.NotNull(externalHttpEndPoint, "externalHttpEndPoint");
            Ensure.NotNull(httpPrefixes, "httpPrefixes");
            if (internalSecureTcpEndPoint != null || externalSecureTcpEndPoint != null)
                Ensure.NotNull(certificate, "certificate");
            Ensure.Positive(workerThreads, "workerThreads");
            Ensure.NotNull(clusterDns, "clusterDns");
            Ensure.NotNull(gossipSeeds, "gossipSeeds");
            Ensure.Positive(clusterNodeCount, "clusterNodeCount");
            Ensure.Positive(prepareAckCount, "prepareAckCount");
            Ensure.Positive(commitAckCount, "commitAckCount");

            if (discoverViaDns && string.IsNullOrWhiteSpace(clusterDns))
                throw new ArgumentException("Either DNS Discovery must be disabled (and seeds specified), or a cluster DNS name must be provided.");

            if (useSsl)
                Ensure.NotNull(sslTargetHost, "sslTargetHost");

            NodeInfo = new VNodeInfo(instanceId, debugIndex,
                                     internalTcpEndPoint, internalSecureTcpEndPoint,
                                     externalTcpEndPoint, externalSecureTcpEndPoint,
                                     internalHttpEndPoint, externalHttpEndPoint);
            HttpPrefixes = httpPrefixes;
            EnableTrustedAuth = enableTrustedAuth;
            Certificate = certificate;
            WorkerThreads = workerThreads;
            DevelopmentMode = developmentMode;

            DiscoverViaDns = discoverViaDns;
            ClusterDns = clusterDns;
            GossipSeeds = gossipSeeds;

            ClusterNodeCount = clusterNodeCount;
            MinFlushDelay = minFlushDelay;
            PrepareAckCount = prepareAckCount;
            CommitAckCount = commitAckCount;
            PrepareTimeout = prepareTimeout;
            CommitTimeout = commitTimeout;

            UseSsl = useSsl;
            SslTargetHost = sslTargetHost;
            SslValidateServer = sslValidateServer;

            StatsPeriod = statsPeriod;
            StatsStorage = statsStorage;

            AuthenticationProviderFactory = authenticationProviderFactory;

            NodePriority = nodePriority;
            DisableScavengeMerging = disableScavengeMerging;
            AdminOnPublic = adminOnPublic;
            StatsOnPublic = statsOnPublic;
            GossipOnPublic = gossipOnPublic;
            GossipInterval = gossipInterval;
            GossipAllowedTimeDifference = gossipAllowedTimeDifference;
            GossipTimeout = gossipTimeout;
            IntTcpHeartbeatTimeout = intTcpHeartbeatTimeout;
            IntTcpHeartbeatInterval = intTcpHeartbeatInterval;
            ExtTcpHeartbeatTimeout = extTcpHeartbeatTimeout;
            ExtTcpHeartbeatInterval = extTcpHeartbeatInterval;

            VerifyDbHash = verifyDbHash;
            MaxMemtableEntryCount = maxMemtableEntryCount;
            EnableHistograms = enableHistograms;
            IndexCacheDepth = indexCacheDepth;
        }

        public override string ToString()
        {
            return string.Format("InstanceId: {0}\n"
                                 + "InternalTcp: {1}\n"
                                 + "InternalSecureTcp: {2}\n"
                                 + "ExternalTcp: {3}\n"
                                 + "ExternalSecureTcp: {4}\n"
                                 + "InternalHttp: {5}\n"
                                 + "ExternalHttp: {6}\n"
                                 + "HttpPrefixes: {7}\n"
                                 + "EnableTrustedAuth: {8}\n"
                                 + "Certificate: {9}\n"
                                 + "WorkerThreads: {10}\n"
                                 + "DiscoverViaDns: {11}\n"
                                 + "ClusterDns: {12}\n"
                                 + "GossipSeeds: {13}\n"
                                 + "ClusterNodeCount: {14}\n"
                                 + "MinFlushDelay: {15}\n"
                                 + "PrepareAckCount: {16}\n"
                                 + "CommitAckCount: {17}\n"
                                 + "PrepareTimeout: {18}\n"
                                 + "CommitTimeout: {19}\n"
                                 + "UseSsl: {20}\n"
                                 + "SslTargetHost: {21}\n"
                                 + "SslValidateServer: {22}\n"
                                 + "StatsPeriod: {23}\n"
                                 + "StatsStorage: {24}\n"
                                 + "AuthenticationProviderFactory Type: {25}\n"
                                 + "NodePriority: {26}"
                                 + "GossipInterval: {27}\n"
                                 + "GossipAllowedTimeDifference: {28}\n"
                                 + "GossipTimeout: {29}\n",
                                 NodeInfo.InstanceId,
                                 NodeInfo.InternalTcp, NodeInfo.InternalSecureTcp,
                                 NodeInfo.ExternalTcp, NodeInfo.ExternalSecureTcp,
                                 NodeInfo.InternalHttp, NodeInfo.ExternalHttp,
                                 string.Join(", ", HttpPrefixes),
                                 EnableTrustedAuth,
                                 Certificate == null ? "n/a" : Certificate.ToString(true),
                                 WorkerThreads, DiscoverViaDns, ClusterDns,
                                 string.Join(",", GossipSeeds.Select(x => x.ToString())),
                                 ClusterNodeCount, MinFlushDelay,
                                 PrepareAckCount, CommitAckCount, PrepareTimeout, CommitTimeout,
                                 UseSsl, SslTargetHost, SslValidateServer,
                                 StatsPeriod, StatsStorage, AuthenticationProviderFactory.GetType(),
                                 NodePriority, GossipInterval, GossipAllowedTimeDifference, GossipTimeout);
        }
    }
}
