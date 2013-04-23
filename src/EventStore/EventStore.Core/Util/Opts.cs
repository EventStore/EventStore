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
// 

using System.Net;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Util
{
    public static class Opts
    {
        public const string EnvPrefix = "EVENTSTORE_";

        /*
         *  COMMON OPTIONS 
         */

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

        public const string DbPathCmd = "d|db=";
        public const string DbPathEnv = "DB";
        public const string DbPathJson = "db";
        public const string DbPathDescr = "The path the db should be loaded/saved to.";
        public static readonly string DbPathDefault = string.Empty;

        public const string SkipDbVerifyCmd = "do-not-verify-db-hashes-on-startup|skip-db-verify";
        public const string SkipDbVerifyEnv = "SKIP_DB_VERIFY";
        public const string SkipDbVerifyJson = "skipDbVerify";
        public const string SkipDbVerifyDescr = "Bypasses the checking of file hashes of database during startup (allows for faster startup).";
        public const bool   SkipDbVerifyDefault = false;

        public const string RunProjectionsCmd = "run-projections";
        public const string RunProjectionsEnv = "RUN_PROJECTIONS";
        public const string RunProjectionsJson = "runProjections";
        public const string RunProjectionsDescr = "Enables the running of JavaScript projections (experimental).";
        public const bool   RunProjectionsDefault = false;

        public const string ProjectionThreadsCmd = "projection-threads=";
        public const string ProjectionThreadsEnv = "PROJECTION_THREADS";
        public const string ProjectionThreadsJson = "projectionThreads";
        public const string ProjectionThreadsDescr = "The number of threads to use for projections.";
        public const int    ProjectionThreadsDefault = 3;

        public const string TcpSendThreadsCmd = "tcp-send-threads=";
        public const string TcpSendThreadsEnv = "TCP_SEND_THREADS";
        public const string TcpSendThreadsJson = "tcpSendThreads";
        public const string TcpSendThreadsDescr = "The number of threads to use for sending to TCP sockets.";
        public const int    TcpSendThreadsDefault = 3;

        public const string HttpReceiveThreadsCmd = "http-receive-threads=";
        public const string HttpReceiveThreadsEnv = "HTTP_RECEIVE_THREADS";
        public const string HttpReceiveThreadsJson = "httpReceiveThreads";
        public const string HttpReceiveThreadsDescr = "The number of threads to use for receiving from HTTP.";
        public const int    HttpReceiveThreadsDefault = 5;

        public const string HttpSendThreadsCmd = "http-send-threads=";
        public const string HttpSendThreadsEnv = "HTTP_SEND_THREADS";
        public const string HttpSendThreadsJson = "httpSendThreads";
        public const string HttpSendThreadsDescr = "The number of threads for sending over HTTP.";
        public const int    HttpSendThreadsDefault = 3;

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
        public static readonly int InternalHttpPortDefault = 2112;

        public const string ExternalHttpPortCmd = "ext-http-port|external-http-port=";
        public const string ExternalHttpPortEnv = "EXT_HTTP_PORT";
        public const string ExternalHttpPortJson = "externalHttpPort";
        public const string ExternalHttpPortDescr = "External HTTP Port.";
        public static readonly int ExternalHttpPortDefault = 2113;

        public const string InternalTcpPortCmd = "int-tcp-port|internal-tcp-port=";
        public const string InternalTcpPortEnv = "INT_TCP_PORT";
        public const string InternalTcpPortJson = "internalTcpPort";
        public const string InternalTcpPortDescr = "Internal TCP Port.";
        public static readonly int InternalTcpPortDefault = 1112;

        public const string ExternalTcpPortCmd = "ext-tcp-port|external-tcp-port=";
        public const string ExternalTcpPortEnv = "EXT_TCP_PORT";
        public const string ExternalTcpPortJson = "externalTcpPort";
        public const string ExternalTcpPortDescr = "External TCP Port.";
        public static readonly int ExternalTcpPortDefault = 1113;

        public const string ClusterDnsCmd = "cluster-dns=";
        public const string ClusterDnsEnv = "CLUSTER_DNS";
        public const string ClusterDnsJson = "clusterDns";
        public const string ClusterDnsDescr = null;
        public static readonly string ClusterDnsDefault = "fake.dns";

        public const string ClusterSizeCmd = "nodes-count|cluster-size=";
        public const string ClusterSizeEnv = "CLUSTER_SIZE";
        public const string ClusterSizeJson = "clusterSize";
        public const string ClusterSizeDescr = null;
        public const int    ClusterSizeDefault = 3;

        public const string CommitCountCmd = "commit-count=";
        public const string CommitCountEnv = "COMMIT_COUNT";
        public const string CommitCountJson = "commitCount";
        public const string CommitCountDescr = null;
        public const int    CommitCountDefault = 2;

        public const string PrepareCountCmd = "prepare-count=";
        public const string PrepareCountEnv = "PREPARE_COUNT";
        public const string PrepareCountJson = "prepareCount";
        public const string PrepareCountDescr = null;
        public const int    PrepareCountDefault = 2;

        public const string FakeDnsCmd = "f|fake-dns";
        public const string FakeDnsEnv = "FAKE_DNS";
        public const string FakeDnsJson = "fakeDns";
        public const string FakeDnsDescr = null;
        public const bool   FakeDnsDefault = true;

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

        public const string FakeDnsIpsCmd = "fake-dns-ip=";
        public const string FakeDnsIpsEnv = "FAKE_DNS_IPS";
        public const string FakeDnsIpsJson = "fakeDnsIps";
        public const string FakeDnsIpsDescr = null;
        public static readonly IPAddress[] FakeDnsIpsDefault = new IPAddress[0];
    }
}