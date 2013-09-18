using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;
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

        public readonly IPEndPoint ManagerEndPoint;
        public readonly string ClusterDns;
        public readonly int ClusterNodeCount;
        public readonly bool FakeDns;

        public readonly int MinFlushDelayMs;
        public readonly int PrepareAckCount;
        public readonly int CommitAckCount;
        public readonly TimeSpan PrepareTimeout;
        public readonly TimeSpan CommitTimeout;

        public readonly bool UseSsl;
        public readonly string SslTargetHost;
        public readonly bool SslValidateServer;

        public readonly TimeSpan StatsPeriod;
        public readonly StatsStorage StatsStorage;

	    public readonly ClusterVNodeAuthenticationType AuthenticationType;

        public ClusterVNodeSettings(Guid instanceId,
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
                                    IPEndPoint managerEndPoint,
                                    string clusterDns,
                                    int clusterNodeCount,
                                    bool fakeDns,
                                    int minFlushDelayMs,
                                    int prepareAckCount,
                                    int commitAckCount,
                                    TimeSpan prepareTimeout,
                                    TimeSpan commitTimeout,
                                    bool useSsl,
                                    string sslTargetHost,
                                    bool sslValidateServer,
                                    TimeSpan statsPeriod,
                                    StatsStorage statsStorage,
									ClusterVNodeAuthenticationType authenticationType)
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
            Ensure.NotNull(managerEndPoint, "managerEndPoint");
            Ensure.NotNull(clusterDns, "clusterDns");
            Ensure.Positive(clusterNodeCount, "clusterNodeCount");
            Ensure.Nonnegative(minFlushDelayMs, "minFlushDelayMs");
            Ensure.Positive(prepareAckCount, "prepareAckCount");
            Ensure.Positive(commitAckCount, "commitAckCount");
            
			if (useSsl)
				Ensure.NotNull(sslTargetHost, "sslTargetHost");

            NodeInfo = new VNodeInfo(instanceId,
                                     internalTcpEndPoint, internalSecureTcpEndPoint,
                                     externalTcpEndPoint, externalSecureTcpEndPoint,
                                     internalHttpEndPoint, externalHttpEndPoint);
            HttpPrefixes = httpPrefixes;
            EnableTrustedAuth = enableTrustedAuth;
            Certificate = certificate;
            WorkerThreads = workerThreads;
            
            ManagerEndPoint = managerEndPoint;
            ClusterDns = clusterDns;
            ClusterNodeCount = clusterNodeCount;
            FakeDns = fakeDns;

            MinFlushDelayMs = minFlushDelayMs;
            PrepareAckCount = prepareAckCount;
            CommitAckCount = commitAckCount;
            PrepareTimeout = prepareTimeout;
            CommitTimeout = commitTimeout;

            UseSsl = useSsl;
            SslTargetHost = sslTargetHost;
            SslValidateServer = sslValidateServer;

            StatsPeriod = statsPeriod;
            StatsStorage = statsStorage;

	        AuthenticationType = authenticationType;
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
                                 + "ManagerEndPoint: {11}\n"
                                 + "ClusterDns: {12}\n"
                                 + "ClusterNodeCount: {13}\n"
                                 + "FakeDns: {14}\n"
                                 + "MinFlushDelayMs: {15}\n"
                                 + "PrepareAckCount: {16}\n"
                                 + "CommitAckCount: {17}\n"
                                 + "PrepareTimeout: {18}\n"
                                 + "CommitTimeout: {19}\n"
                                 + "UseSsl: {20}\n"
                                 + "SslTargetHost: {21}\n"
                                 + "SslValidateServer: {22}\n"
                                 + "StatsPeriod: {23}\n"
                                 + "StatsStorage: {24}\n"
								 + "AuthenticationType: {25}",
                                 NodeInfo.InstanceId,
                                 NodeInfo.InternalTcp, NodeInfo.InternalSecureTcp,
                                 NodeInfo.ExternalTcp, NodeInfo.ExternalSecureTcp,
                                 NodeInfo.InternalHttp, NodeInfo.ExternalHttp,
                                 string.Join(", ", HttpPrefixes),
                                 EnableTrustedAuth,
                                 Certificate == null ? "n/a" : Certificate.ToString(true),
                                 WorkerThreads,
                                 ManagerEndPoint,
                                 ClusterDns, ClusterNodeCount, FakeDns,
                                 MinFlushDelayMs,
                                 PrepareAckCount, CommitAckCount, PrepareTimeout, CommitTimeout,
                                 UseSsl, SslTargetHost, SslValidateServer,
                                 StatsPeriod, StatsStorage, AuthenticationType);
        }
    }
}
