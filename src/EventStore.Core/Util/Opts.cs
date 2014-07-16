using System.Net;
using EventStore.Common.Options;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Util
{
    public static class Opts
    {
        public const string EnvPrefix = "EVENTSTORE_";

        /*
         * OPTIONS GROUPS
         */
        public const string AppGroup = "Application Options";
        public const string DbGroup = "Database Options";
        public const string ProjectionsGroup = "Projections Options";
        public const string AuthGroup = "Authentication Options";
        public const string InterfacesGroup = "Interface Options";
        public const string CertificatesGroup = "Certificate Options";
        public const string ClusterGroup = "Cluster Options";
        public const string ManagerGroup = "Manager Options";

        /*
         *  COMMON OPTIONS 
         */

        public const string ForceDescr = "Force the Event Store to run in possibly harmful environments such as with Boehm GC.";
        public const bool ForceDefault = false;

        public const string WhatIfDescr = "Print effective configuration to console and then exit.";
        public const bool WhatIfDefault = false;

        public const string LogsDescr = "Path where to keep log files.";
        public static readonly string LogsDefault = string.Empty;

        public const string ConfigsDescr = "Configuration files.";
        public static readonly string[] ConfigsDefault = new string[0];

        public const string DefinesDescr = "Run-time conditionals.";
        public static readonly string[] DefinesDefault = new string[0];

        public const string ShowHelpDescr = "Show help.";
        public const bool   ShowHelpDefault = false;

        public const string ShowVersionDescr = "Show version.";
        public const bool   ShowVersionDefault = false;

        public const string TcpTimeoutDescr = "Timeout for TCP sockets";
        public const int TcpTimeoutDefault = 1000;

        public const string StatsPeriodDescr = "The number of seconds between statistics gathers.";
        public const int    StatsPeriodDefault = 30;

        public const string CachedChunksDescr = "The number of chunks to cache in unmanaged memory.";
        public const int    CachedChunksDefault = -1;

        public const string ChunksCacheSizeDescr = "The amount of unmanaged memory to use for caching chunks.";
        public const int    ChunksCacheSizeDefault = TFConsts.ChunksCacheSize;

        public const string MinFlushDelayMsDescr = "The minimum flush delay in milliseconds.";
        public static double MinFlushDelayMsDefault = TFConsts.MinFlushDelayMs.TotalMilliseconds;

        public const string NodePriorityDescr = "The node priority used during master election";
        public const int    NodePriorityDefault = 0;

        public const string DisableScavengeMergeDescr = "Disables the merging of chunks when scavenge is running";
        public static readonly bool DisableScavengeMergeDefault = false;

        public const string DbPathDescr = "The path the db should be loaded/saved to.";
        public static readonly string DbPathDefault = string.Empty;

        public const string InMemDbDescr = "Keep everything in memory, no directories or files are created.";
        public const bool   InMemDbDefault = false;

        public const string EnableTrustedAuthDescr = "Enables trusted authentication by an intermediary in the Http";
        public const bool EnableTrustedAuthDefault = false;

        public const string MaxMemTableSizeDescr = "Adjusts the maximum size of a mem table.";
        public const int MaxMemtableSizeDefault = 1000000;

        public const string SkipDbVerifyDescr = "Bypasses the checking of file hashes of database during startup (allows for faster startup).";
        public const bool SkipDbVerifyDefault = false;

        public const string RunProjectionsDescr = "Enables the running of JavaScript projections.";
        public const ProjectionType RunProjectionsDefault = ProjectionType.System;

        public const string ProjectionThreadsDescr = "The number of threads to use for projections.";
        public const int    ProjectionThreadsDefault = 3;

        public const string WorkerThreadsDescr = "The number of threads to use for pool of worker services.";
        public const int    WorkerThreadsDefault = 5;

        public const string HttpPrefixesDescr = "The prefixes that the http server should respond to.";
        public static readonly string[] HttpPrefixesDefault = new string[0];

        public const string UnsafeDisableFlushToDiskDescr = "Disable flushing to disk.  (UNSAFE: on power off)";
        public static readonly bool UnsafeDisableFlushToDiskDefault = false; 

        public const string PrepareTimeoutMsDescr = "Prepare timeout (in milliseconds).";
        public static readonly int PrepareTimeoutMsDefault = 2000; // 2 seconds

        public const string CommitTimeoutMsDescr = "Commit timeout (in milliseconds).";
        public static readonly int CommitTimeoutMsDefault = 2000; // 2 seconds

        //Loading certificates from files
        public const string CertificateFileDescr = "The path to certificate file.";
        public static readonly string CertificateFileDefault = string.Empty;

        public const string CertificatePasswordDescr = "The password to certificate in file.";
        public static readonly string CertificatePasswordDefault = string.Empty;

        //Loading certificates from a certificate store
        public const string CertificateStoreLocationDescr = "The certificate store location name.";
        public static readonly string CertificateStoreLocationDefault = string.Empty;

        public const string CertificateStoreNameDescr = "The certificate store name.";
        public static readonly string CertificateStoreNameDefault = string.Empty;

        public const string CertificateSubjectNameDescr = "The certificate subject name.";
        public static readonly string CertificateSubjectNameDefault = string.Empty;

        public const string CertificateThumbprintDescr = "The certificate fingerprint/thumbprint.";
        public static readonly string CertificateThumbprintDefault = string.Empty;
        
        /*
         *  SINGLE NODE OPTIONS
         */
        public const string IpDescr = "The IP address to bind to.";
        public static readonly IPAddress IpDefault = IPAddress.Loopback;

        public const string TcpPortDescr = "The port to run the TCP server on.";
        public const int    TcpPortDefault = 1113;

        public const string SecureTcpPortDescr = "The port to run the secure TCP server on.";
        public const int    SecureTcpPortDefault = 0;

        public const string HttpPortDescr = "The port to run the HTTP server on.";
        public const int    HttpPortDefault = 2113;

        /*
         *  CLUSTER OPTIONS
         */
        public const string GossipAllowedDifferenceMsDescr = "The amount of drift between clocks on nodes allowed before gossip is rejected in ms.";
        public const int GossipAllowedDifferenceMsDefault = 60000;

        public const string GossipIntervalMsDescr = "The interval nodes should try to gossip with each other in ms.";
        public const int GossipIntervalMsDefault = 1000;

        public const string GossipTimeoutMsDescr = "The timeout on gossip to another node in ms.";
        public const int GossipTimeoutMsDefault = 500;

        public const string AdminOnExtDescr = "Whether or not to run the admin ui on the external http endpoint";
        public const bool AdminOnExtDefault = true;

        public const string GossipOnExtDescr = "Whether or not to accept gossip requests on the external http endpoint";
        public const bool GossipOnExtDefault = true;

        public const string StatsOnExtDescr = "Whether or not to accept statistics requests on the external http endpoint, needed if you use admin ui";
        public const bool StatsOnExtDefault = true;

        public const string InternalIpDescr = "Internal IP Address.";
        public static readonly IPAddress InternalIpDefault = IPAddress.Loopback;

        public const string ExternalIpDescr = "External IP Address.";
        public static readonly IPAddress ExternalIpDefault = IPAddress.Loopback;

        public const string InternalHttpPortDescr = "Internal HTTP Port.";
        public const int    InternalHttpPortDefault = 2112;

        public const string ExternalHttpPortDescr = "External HTTP Port.";
        public const int    ExternalHttpPortDefault = 2113;

        public const string InternalTcpPortDescr = "Internal TCP Port.";
        public const int    InternalTcpPortDefault = 1112;

        public const string InternalSecureTcpPortDescr = "Internal Secure TCP Port.";
        public const int    InternalSecureTcpPortDefault = 0;

        public const string ExternalTcpPortDescr = "External TCP Port.";
        public const int    ExternalTcpPortDefault = 1113;

        public const string ExternalSecureTcpPortDescr = "External Secure TCP Port.";
        public const int    ExternalSecureTcpPortDefault = 0;

		public const string ClusterSizeDescr = "The number of nodes in the cluster.";
        public const int    ClusterSizeDefault = 3;

        public const string CommitCountDescr = "The number of nodes which must acknowledge commits before acknowledging to a client.";
        public const int    CommitCountDefault = -1;

        public const string PrepareCountDescr = "The number of nodes which must acknowledge prepares.";	
        public const int    PrepareCountDefault = -1;

        public const string InternalManagerIpDescr = null;
        public static readonly IPAddress InternalManagerIpDefault = IPAddress.Loopback;

        public const string ExternalManagerIpDescr = null;
        public static readonly IPAddress ExternalManagerIpDefault = IPAddress.Loopback;

        public const string InternalManagerHttpPortDescr = null;
        public const int    InternalManagerHttpPortDefault = 30777;

        public const string ExternalManagerHttpPortDescr = null;
        public const int    ExternalManagerHttpPortDefault = 30778;

        public const string UseInternalSslDescr = "Whether to use secure internal communication.";
        public const bool   UseInternalSslDefault = false;

        public const string SslTargetHostDescr = "Target host of server's SSL certificate.";
        public static readonly string SslTargetHostDefault = "n/a";

        public const string SslValidateServerDescr = "Whether to validate that server's certificate is trusted.";
        public const bool   SslValidateServerDefault = true;

		public const string DiscoverViaDnsDescr = "Whether to use DNS lookup to discover other cluster nodes.";
 	    public const bool DiscoverViaDnsDefault = true;
 
 		public const string ClusterDnsDescr = "DNS name from which other nodes can be discovered.";
 		public const string ClusterDnsDefault = "fake.dns";

	    public const int ClusterGossipPortDefault = 30777;
	    public const string ClusterGossipPortDescr = "The port on which cluster nodes' managers are running.";

 	    public const string GossipSeedDescr = "Endpoints for other cluster nodes from which to seed gossip";
 		public static readonly IPEndPoint[] GossipSeedDefault = new IPEndPoint[0];

        /*
         *  MANAGER OPTIONS 
         */
        public const string EnableWatchdogDescr = null;
        public const bool   EnableWatchdogDefault = true;

        public const string WatchdogConfigDescr = null;
        public static readonly string WatchdogConfigDefault = string.Empty;

        public const string WatchdogStateDescr = null;
        public static readonly string WatchdogStateDefault = string.Empty;

        public const string WatchdogFailureTimeWindowDescr = "The time window for which to track supervised node failures.";
        public static readonly int WatchdogFailureTimeWindowDefault = -1;

        public const string WatchdogFailureCountDescr = "The maximum allowed supervised node failures within specified time window.";
        public static readonly int WatchdogFailureCountDefault = -1;

		/*
		 * Authentication Options
		 */
	    public const string AuthenticationTypeDescr = "The type of authentication to use.";
	    public static readonly string AuthenticationTypeDefault = "internal";

	    public const string AuthenticationConfigFileDescr = "Path to the configuration file for authentication configuration (if applicable).";
		public static readonly string AuthenticationConfigFileDefault = string.Empty;
    }
}
