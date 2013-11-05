// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System.Net;
using EventStore.Common.Options;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Util
{
    public static class Opts
    {
        public const string EnvPrefix = "EVENTSTORE_";

        /*
         *  COMMON OPTIONS 
         */

        public const string ForceCmd = "force";
        public const string ForceEnv = null;
        public const string ForceJson = null;
        public const string ForceDescr = "Force the Event Store to run in possibly harmful environments such as with Boehm GC.";
        public const bool ForceDefault = false;

        public const string LogsCmd = "log|logsdir=";
        public const string LogsEnv = "LOGSDIR";
        public const string LogsJson = "logsdir";
        public const string LogsDescr = "Path where to keep log files.";
        public static readonly string LogsDefault = string.Empty;

        public const string ConfigsCmd = "cfg|config=";
        public const string ConfigsEnv = "CONFIGS";
        public const string ConfigsJson = "configs";
        public const string ConfigsDescr = "Configuration files.";
        public static readonly string[] ConfigsDefault = new string[0];

        public const string DefinesCmd = "def|define=";
        public const string DefinesEnv = "DEFINES";
        public const string DefinesJson = "defines";
        public const string DefinesDescr = "Run-time conditionals.";
        public static readonly string[] DefinesDefault = new string[0];

        public const string ShowHelpCmd = "?|help";
        public const string ShowHelpEnv = null;
        public const string ShowHelpJson = null;
        public const string ShowHelpDescr = "Show help.";
        public const bool   ShowHelpDefault = false;

        public const string ShowVersionCmd = "version";
        public const string ShowVersionEnv = null;
        public const string ShowVersionJson = null;
        public const string ShowVersionDescr = "Show version.";
        public const bool   ShowVersionDefault = false;

        public const string StatsPeriodCmd = "s|stats-period-sec=";
        public const string StatsPeriodEnv = "STATS_PERIOD_SEC";
        public const string StatsPeriodJson = "statsPeriodSec";
        public const string StatsPeriodDescr = "The number of seconds between statistics gathers.";
        public const int    StatsPeriodDefault = 30;

        public const string CachedChunksCmd = "c|chunkcache|cached-chunks=";
        public const string CachedChunksEnv = "CACHED_CHUNKS";
        public const string CachedChunksJson = "cachedChunks";
        public const string CachedChunksDescr = "The number of chunks to cache in unmanaged memory.";
        public const int    CachedChunksDefault = -1;

        public const string ChunksCacheSizeCmd = "chunks-cache-size=";
        public const string ChunksCacheSizeEnv = "CHUNKS_CACHE_SIZE";
        public const string ChunksCacheSizeJson = "chunksCacheSize";
        public const string ChunksCacheSizeDescr = "The amount of unmanaged memory to use for caching chunks.";
        public const int    ChunksCacheSizeDefault = TFConsts.ChunksCacheSize;

        public const string MinFlushDelayMsCmd = "min-flush-delay=";
        public const string MinFlushDelayMsEnv = "MIN_FLUSH_DELAY";
        public const string MinFlushDelayMsJson = "minFlushDelay";
        public const string MinFlushDelayMsDescr = "The minimum flush delay in milliseconds.";
        public static double MinFlushDelayMsDefault = TFConsts.MinFlushDelayMs.TotalMilliseconds;

        public const string NodePriorityCmd = "node-priority=";
        public const string NodePriorityEnv = "NODE_PRIORITY";
        public const string NodePriorityJson = "nodePriority";
        public const string NodePriorityDescr = "The node priority used during master election";
        public const int    NodePriorityDefault = 0;

        public const string DisableScavengeMergeCmd = "nomerge";
        public const string DisableScavengeMergeEnv = "NO_MERGE";
        public const string DisableScavengeMergeJson = "noMerge";
        public const string DisableScavengeMergeDescr = "Disables the merging of chunks when scavenge is running";
        public static readonly bool DisableScavengeMergeDefault = false;


        public const string DbPathCmd = "d|db=";
        public const string DbPathEnv = "DB";
        public const string DbPathJson = "db";
        public const string DbPathDescr = "The path the db should be loaded/saved to.";
        public static readonly string DbPathDefault = string.Empty;

        public const string InMemDbCmd = "mem-db";
        public const string InMemDbEnv = "MEM_DB";
        public const string InMemDbJson = "memDb";
        public const string InMemDbDescr = "Keep everything in memory, no directories or files are created.";
        public const bool   InMemDbDefault = false;

        public const string EnableTrustedAuthCmd = "enable-trusted-auth";
        public const string EnableTrustedAuthEnv = "ENABLE_TRUSTED_AUTH";
        public const string EnableTrustedAuthJson = "enableTrustedAuth";
        public const string EnableTrustedAuthDescr = "Enables trusted authentication by an intermediary in the Http";
        public const bool EnableTrustedAuthDefault = false;

        public const string SkipDbVerifyCmd = "do-not-verify-db-hashes-on-startup|skip-db-verify";
        public const string SkipDbVerifyEnv = "SKIP_DB_VERIFY";
        public const string SkipDbVerifyJson = "skipDbVerify";
        public const string SkipDbVerifyDescr = "Bypasses the checking of file hashes of database during startup (allows for faster startup).";
        public const bool SkipDbVerifyDefault = false;

        public const string RunProjectionsCmd = "run-projections=";
        public const string RunProjectionsEnv = "RUN_PROJECTIONS";
        public const string RunProjectionsJson = "runProjections";
        public const string RunProjectionsDescr = "Enables the running of JavaScript projections.";
        public const RunProjections RunProjectionsDefault = RunProjections.System;

        public const string ProjectionThreadsCmd = "projection-threads=";
        public const string ProjectionThreadsEnv = "PROJECTION_THREADS";
        public const string ProjectionThreadsJson = "projectionThreads";
        public const string ProjectionThreadsDescr = "The number of threads to use for projections.";
        public const int    ProjectionThreadsDefault = 3;

        public const string WorkerThreadsCmd = "worker-threads=";
        public const string WorkerThreadsEnv = "WORKER_THREADS";
        public const string WorkerThreadsJson = "workerThreads";
        public const string WorkerThreadsDescr = "The number of threads to use for pool of worker services.";
        public const int    WorkerThreadsDefault = 5;

        public const string HttpPrefixesCmd = "prefixes|http-prefix=";
        public const string HttpPrefixesEnv = "HTTP_PREFIXES";
        public const string HttpPrefixesJson = "httpPrefixes";
        public const string HttpPrefixesDescr = "The prefixes that the http server should respond to.";
        public static readonly string[] HttpPrefixesDefault = new string[0];

        public const string PrepareTimeoutMsCmd = "pt|prepare-timeout=";
        public const string PrepareTimeoutMsEnv = "PREPARE_TIMEOUT_MS";
        public const string PrepareTimeoutMsJson = "prepareTimeoutMs";
        public const string PrepareTimeoutMsDescr = "Prepare timeout (in milliseconds).";
        public static readonly int PrepareTimeoutMsDefault = 2000; // 2 seconds

        public const string CommitTimeoutMsCmd = "ct|commit-timeout=";
        public const string CommitTimeoutMsEnv = "COMMIT_TIMEOUT_MS";
        public const string CommitTimeoutMsJson = "commitTimeoutMs";
        public const string CommitTimeoutMsDescr = "Commit timeout (in milliseconds).";
        public static readonly int CommitTimeoutMsDefault = 2000; // 2 seconds

        public const string CertificateStoreCmd = "certificate-store=";
        public const string CertificateStoreEnv = "CERTIFICATE_STORE";
        public const string CertificateStoreJson = "certificateStore";
        public const string CertificateStoreDescr = "The name of certificate store.";
        public static readonly string CertificateStoreDefault = string.Empty;

        public const string CertificateNameCmd = "certificate-name=";
        public const string CertificateNameEnv = "CERTIFICATE_NAME";
        public const string CertificateNameJson = "certificateName";
        public const string CertificateNameDescr = "The name of certificate in store.";
        public static readonly string CertificateNameDefault = string.Empty;

        public const string CertificateFileCmd = "certificate-file=";
        public const string CertificateFileEnv = "CERTIFICATE_FILE";
        public const string CertificateFileJson = "certificateFile";
        public const string CertificateFileDescr = "The path to certificate file.";
        public static readonly string CertificateFileDefault = string.Empty;

        public const string CertificatePasswordCmd = "certificate-password=";
        public const string CertificatePasswordEnv = "CERTIFICATE_PASSWORD";
        public const string CertificatePasswordJson = "certificatePassword";
        public const string CertificatePasswordDescr = "The password to certificate in file.";
        public static readonly string CertificatePasswordDefault = string.Empty;

        /*
         *  SINGLE NODE OPTIONS
         */
        public const string IpCmd = "i|ip=";
        public const string IpEnv = "IP";
        public const string IpJson = "ip";
        public const string IpDescr = "The IP address to bind to.";
        public static readonly IPAddress IpDefault = IPAddress.Loopback;

        public const string TcpPortCmd = "t|tcp-port=";
        public const string TcpPortEnv = "TCP_PORT";
        public const string TcpPortJson = "tcpPort";
        public const string TcpPortDescr = "The port to run the TCP server on.";
        public const int    TcpPortDefault = 1113;

        public const string SecureTcpPortCmd = "st|sec-tcp-port|secure-tcp-port=";
        public const string SecureTcpPortEnv = "SEC_TCP_PORT";
        public const string SecureTcpPortJson = "secureTcpPort";
        public const string SecureTcpPortDescr = "The port to run the secure TCP server on.";
        public const int    SecureTcpPortDefault = 0;

        public const string HttpPortCmd = "h|http-port=";
        public const string HttpPortEnv = "HTTP_PORT";
        public const string HttpPortJson = "httpPort";
        public const string HttpPortDescr = "The port to run the HTTP server on.";
        public const int    HttpPortDefault = 2113;

        /*
         *  CLUSTER OPTIONS
         */
        public const string InternalIpCmd = "int-ip|internal-ip=";
        public const string InternalIpEnv = "INT_IP";
        public const string InternalIpJson = "internalIp";
        public const string InternalIpDescr = "Internal IP Address.";
        public static readonly IPAddress InternalIpDefault = IPAddress.Loopback;

        public const string ExternalIpCmd = "ext-ip|external-ip=";
        public const string ExternalIpEnv = "EXT_IP";
        public const string ExternalIpJson = "externalIp";
        public const string ExternalIpDescr = "External IP Address.";
        public static readonly IPAddress ExternalIpDefault = IPAddress.Loopback;

        public const string InternalHttpPortCmd = "int-http-port|internal-http-port=";
        public const string InternalHttpPortEnv = "INT_HTTP_PORT";
        public const string InternalHttpPortJson = "internalHttpPort";
        public const string InternalHttpPortDescr = "Internal HTTP Port.";
        public const int    InternalHttpPortDefault = 2112;

        public const string ExternalHttpPortCmd = "ext-http-port|external-http-port=";
        public const string ExternalHttpPortEnv = "EXT_HTTP_PORT";
        public const string ExternalHttpPortJson = "externalHttpPort";
        public const string ExternalHttpPortDescr = "External HTTP Port.";
        public const int    ExternalHttpPortDefault = 2113;

        public const string InternalTcpPortCmd = "int-tcp-port|internal-tcp-port=";
        public const string InternalTcpPortEnv = "INT_TCP_PORT";
        public const string InternalTcpPortJson = "internalTcpPort";
        public const string InternalTcpPortDescr = "Internal TCP Port.";
        public const int    InternalTcpPortDefault = 1112;

        public const string InternalSecureTcpPortCmd = "int-sec-tcp-port|internal-secure-tcp-port=";
        public const string InternalSecureTcpPortEnv = "INT_SEC_TCP_PORT";
        public const string InternalSecureTcpPortJson = "internalSecureTcpPort";
        public const string InternalSecureTcpPortDescr = "Internal Secure TCP Port.";
        public const int    InternalSecureTcpPortDefault = 0;

        public const string ExternalTcpPortCmd = "ext-tcp-port|external-tcp-port=";
        public const string ExternalTcpPortEnv = "EXT_TCP_PORT";
        public const string ExternalTcpPortJson = "externalTcpPort";
        public const string ExternalTcpPortDescr = "External TCP Port.";
        public const int    ExternalTcpPortDefault = 1113;

        public const string ExternalSecureTcpPortCmd = "ext-sec-tcp-port|external-secure-tcp-port=";
        public const string ExternalSecureTcpPortEnv = "EXT_SEC_TCP_PORT";
        public const string ExternalSecureTcpPortJson = "externalSecureTcpPort";
        public const string ExternalSecureTcpPortDescr = "External Secure TCP Port.";
        public const int    ExternalSecureTcpPortDefault = 0;

		public const string ClusterSizeCmd = "nodes-count|cluster-size=";
        public const string ClusterSizeEnv = "CLUSTER_SIZE";
        public const string ClusterSizeJson = "clusterSize";
		public const string ClusterSizeDescr = "The number of nodes in the cluster.";
        public const int    ClusterSizeDefault = 3;

        public const string CommitCountCmd = "commit-count=";
        public const string CommitCountEnv = "COMMIT_COUNT";
        public const string CommitCountJson = "commitCount";
		public const string CommitCountDescr = "The number of nodes which must acknowledge commits before acknowledging to a client.";
        public const int    CommitCountDefault = 2;

        public const string PrepareCountCmd = "prepare-count=";
        public const string PrepareCountEnv = "PREPARE_COUNT";
        public const string PrepareCountJson = "prepareCount";
		public const string PrepareCountDescr = "The number of nodes which must acknowledge prepares.";	
        public const int    PrepareCountDefault = 2;

        public const string InternalManagerIpCmd = "manager-ip|int-manager-ip|internal-manager-ip=";
        public const string InternalManagerIpEnv = "INT_MANAGER_IP";
        public const string InternalManagerIpJson = "internalManagerIp";
        public const string InternalManagerIpDescr = null;
        public static readonly IPAddress InternalManagerIpDefault = IPAddress.Loopback;

        public const string ExternalManagerIpCmd = "ext-manager-ip|external-manager-ip=";
        public const string ExternalManagerIpEnv = "EXT_MANAGER_IP";
        public const string ExternalManagerIpJson = "externalManagerIp";
        public const string ExternalManagerIpDescr = null;
        public static readonly IPAddress ExternalManagerIpDefault = IPAddress.Loopback;

        public const string InternalManagerHttpPortCmd = "manager-port|int-manager-http-port|internal-manager-http-port=";
        public const string InternalManagerHttpPortEnv = "INT_MANAGER_HTTP_PORT";
        public const string InternalManagerHttpPortJson = "internalManagerHttpPort";
        public const string InternalManagerHttpPortDescr = null;
        public const int    InternalManagerHttpPortDefault = 30777;

        public const string ExternalManagerHttpPortCmd = "ext-manager-http-port|external-manager-http-port=";
        public const string ExternalManagerHttpPortEnv = "EXT_MANAGER_HTTP_PORT";
        public const string ExternalManagerHttpPortJson = "externalManagerHttpPort";
        public const string ExternalManagerHttpPortDescr = null;
        public const int    ExternalManagerHttpPortDefault = 30778;

        public const string UseInternalSslCmd = "use-internal-ssl";
        public const string UseInternalSslEnv = "USE_INTERNAL_SSL";
        public const string UseInternalSslJson = "useInternalSsl";
        public const string UseInternalSslDescr = "Whether to use secure internal communication.";
        public const bool   UseInternalSslDefault = false;

        public const string SslTargetHostCmd = "ssl-target-host=";
        public const string SslTargetHostEnv = "SSL_TARGET_HOST";
        public const string SslTargetHostJson = "sslTargetHost";
        public const string SslTargetHostDescr = "Target host of server's SSL certificate.";
        public static readonly string SslTargetHostDefault = "n/a";

        public const string SslValidateServerCmd = "ssl-validate-server";
        public const string SslValidateServerEnv = "SSL_VALIDATE_SERVER";
        public const string SslValidateServerJson = "sslValidateServer";
        public const string SslValidateServerDescr = "Whether to validate that server's certificate is trusted.";
        public const bool   SslValidateServerDefault = true;

		public const string DiscoverViaDnsCmd = "use-dns-discovery";
 	    public const string DiscoverViaDnsEnv = "USE_DNS_DISCOVERY";
 	    public const string DiscoverViaDnsJson = "useDnsDiscovery";
 	    public const string DiscoverViaDnsDescr = "Whether to use DNS lookup to discover other cluster nodes.";
 	    public const bool DiscoverViaDnsDefault = true;
 
 		public const string ClusterDnsCmd = "cluster-dns=";
 		public const string ClusterDnsEnv = "CLUSTER_DNS";
 		public const string ClusterDnsJson = "clusterDns";
 		public const string ClusterDnsDescr = "DNS name from which other nodes can be discovered.";
 		public const string ClusterDnsDefault = "fake.dns";

	    public const string ClusterGossipPortCmd = "cluster-gossip-port=";
	    public const string ClusterGossipPortEnv = "CLUSTER_GOSSIP_PORT";
	    public const string ClusterGossipPortJson = "clusterGossipPort";
	    public const int ClusterGossipPortDefault = 30777;
	    public const string ClusterGossipPortDescr = "The port on which cluster nodes' managers are running.";

 	    public const string GossipSeedCmd = "gossip-seed=";
 	    public const string GossipSeedEnv = "GOSSIP_SEED";
 	    public const string GossipSeedJson = "gossipSeed";
 	    public const string GossipSeedDescr = "Endpoints for other cluster nodes from which to seed gossip";
 		public static readonly IPEndPoint[] GossipSeedDefault = new IPEndPoint[0];

        /*
         *  MANAGER OPTIONS 
         */
        public const string EnableWatchdogCmd = "w|watchdog";
        public const string EnableWatchdogEnv = "WATCHDOG";
        public const string EnableWatchdogJson = "watchdog";
        public const string EnableWatchdogDescr = null;
        public const bool   EnableWatchdogDefault = true;

        public const string WatchdogConfigCmd = "watchdog-config=";
        public const string WatchdogConfigEnv = "WATCHDOG_CONFIG";
        public const string WatchdogConfigJson = "watchdogConfig";
        public const string WatchdogConfigDescr = null;
        public static readonly string WatchdogConfigDefault = string.Empty;

        public const string WatchdogStateCmd = "watchdog-state=";
        public const string WatchdogStateEnv = "WATCHDOG_STATE";
        public const string WatchdogStateJson = "watchdogState";
        public const string WatchdogStateDescr = null;
        public static readonly string WatchdogStateDefault = string.Empty;

        public const string WatchdogFailureTimeWindowCmd = "watchdog-failure-time-window=";
        public const string WatchdogFailureTimeWindowEnv = "WATCHDOG_FAILURE_TIME_WINDOW";
        public const string WatchdogFailureTimeWindowJson = "watchdogFailureTimeWindow";
        public const string WatchdogFailureTimeWindowDescr = "The time window for which to track supervised node failures.";
        public static readonly int WatchdogFailureTimeWindowDefault = -1;

        public const string WatchdogFailureCountCmd = "watchdog-failure-count=";
        public const string WatchdogFailureCountEnv = "WATCHDOG_FAILURE_COUNT";
        public const string WatchdogFailureCountJson = "watchdogFailureCount";
        public const string WatchdogFailureCountDescr = "The maximum allowed supervised node failures within specified time window.";
        public static readonly int WatchdogFailureCountDefault = -1;

		/*
		 * Authentication Options
		 */
	    public const string AuthenticationTypeCmd = "authentication=";
		public const string AuthenticationTypeEnv = "AUTHENTICATION";
		public const string AuthenticationTypeJson = "authentication";
		public const string AuthenticationTypeDescr = "The type of authentication to use.";
	    public static readonly string AuthenticationTypeDefault = "internal";

	    public const string AuthenticationConfigFileCmd = "authentication-config=";
		public const string AuthenticationConfigFileEnv = "AUTHENTICATION_CONFIG";
		public const string AuthenticationConfigFileJson = "authenticationConfig";
		public const string AuthenticationConfigFileDescr = "Path to the configuration file for authentication configuration (if applicable).";
		public static readonly string AuthenticationConfigFileDefault = string.Empty;
    }
}
