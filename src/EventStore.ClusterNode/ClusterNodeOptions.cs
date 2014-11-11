﻿using System.Net;
using EventStore.Common.Options;
using EventStore.Core.Util;
using EventStore.Rags;

namespace EventStore.ClusterNode
{
    public class ClusterNodeOptions : IOptions
    {
        [ArgDescription(Opts.ShowHelpDescr, Opts.AppGroup)]
        public bool Help { get; set; }
        [ArgDescription(Opts.ShowVersionDescr, Opts.AppGroup)]
        public bool Version { get; set; }
        [ArgDescription(Opts.LogsDescr, Opts.AppGroup)]
        public string Log { get; set; }
        [ArgDescription(Opts.ConfigsDescr, Opts.AppGroup)]
        public string Config { get; set; }
        [ArgDescription(Opts.DefinesDescr, Opts.AppGroup)]
        public string[] Defines { get; set; }
        [ArgDescription(Opts.WhatIfDescr, Opts.AppGroup)]
        public bool WhatIf { get; set; }

        [ArgDescription(Opts.InternalIpDescr, Opts.InterfacesGroup)]
        public IPAddress IntIp { get; set; }
        [ArgDescription(Opts.ExternalIpDescr, Opts.InterfacesGroup)]
        public IPAddress ExtIp { get; set; }
        [ArgDescription(Opts.InternalHttpPortDescr, Opts.InterfacesGroup)]
        public int IntHttpPort { get; set; }
        [ArgDescription(Opts.ExternalHttpPortDescr, Opts.InterfacesGroup)]
        public int ExtHttpPort { get; set; }
        [ArgDescription(Opts.InternalTcpPortDescr, Opts.InterfacesGroup)]
        public int IntTcpPort { get; set; }
        [ArgDescription(Opts.InternalSecureTcpPortDescr, Opts.InterfacesGroup)]
        public int IntSecureTcpPort { get; set; }
        [ArgDescription(Opts.ExternalTcpPortDescr, Opts.InterfacesGroup)]
        public int ExtTcpPort { get; set; }
        [ArgDescription(Opts.ExternalSecureTcpPortDescr, Opts.InterfacesGroup)]
        public int ExtSecureTcpPort { get; set; }

        [ArgDescription(Opts.IntTcpHeartbeatTimeoutDescr, Opts.InterfacesGroup)]
        public int IntTcpHeartbeatTimeout {get; set;}
        [ArgDescription(Opts.ExtTcpHeartbeatTimeoutDescr, Opts.InterfacesGroup)]
        public int ExtTcpHeartbeatTimeout { get; set; }
        [ArgDescription(Opts.IntTcpHeartbeatIntervalDescr, Opts.InterfacesGroup)]
        public int IntTcpHeartbeatInterval { get; set; }
        [ArgDescription(Opts.ExtTcpHeartbeatIntervalDescr, Opts.InterfacesGroup)]
        public int ExtTcpHeartbeatInterval { get; set; }


        [ArgDescription(Opts.ForceDescr, Opts.AppGroup)]
        public bool Force { get; set; }
        [ArgDescription(Opts.ClusterSizeDescr, Opts.ClusterGroup)]
        public int ClusterSize { get; set; }
        [ArgDescription(Opts.NodePriorityDescr, Opts.ClusterGroup)]
        public int NodePriority { get; set; }
        [ArgDescription(Opts.MinFlushDelayMsDescr, Opts.DbGroup)]
        public double MinFlushDelayMs { get; set; }

        [ArgDescription(Opts.CommitCountDescr, Opts.ClusterGroup)]
        public int CommitCount { get; set; }
        [ArgDescription(Opts.PrepareCountDescr, Opts.ClusterGroup)]
        public int PrepareCount { get; set; }

        [ArgDescription(Opts.AdminOnExtDescr, Opts.InterfacesGroup)]
        public bool AdminOnExt { get; set; }
        [ArgDescription(Opts.StatsOnExtDescr, Opts.InterfacesGroup)]
        public bool StatsOnExt { get; set; }
        [ArgDescription(Opts.GossipOnExtDescr, Opts.InterfacesGroup)]
        public bool GossipOnExt { get; set; }
        [ArgDescription(Opts.DisableScavengeMergeDescr, Opts.DbGroup)]
        public bool DisableScavengeMerging { get; set; }

        [ArgDescription(Opts.DiscoverViaDnsDescr, Opts.ClusterGroup)]
        public bool DiscoverViaDns { get; set; }
        [ArgDescription(Opts.ClusterDnsDescr, Opts.ClusterGroup)]
        public string ClusterDns { get; set; }
        [ArgDescription(Opts.ClusterGossipPortDescr, Opts.ClusterGroup)]
        public int ClusterGossipPort { get; set; }
        [ArgDescription(Opts.GossipSeedDescr, Opts.ClusterGroup)]
        public IPEndPoint[] GossipSeed { get; set; }

        [ArgDescription(Opts.StatsPeriodDescr, Opts.AppGroup)]
        public int StatsPeriodSec { get; set; }
        [ArgDescription(Opts.CachedChunksDescr, Opts.DbGroup)]
        public int CachedChunks { get; set; }
        [ArgDescription(Opts.ChunksCacheSizeDescr, Opts.DbGroup)]
        public long ChunksCacheSize { get; set; }
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

        [ArgDescription(Opts.HttpPrefixesDescr, Opts.InterfacesGroup)]
        public string[] HttpPrefixes { get; set; }
        [ArgDescription(Opts.EnableTrustedAuthDescr, Opts.InterfacesGroup)]
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

        [ArgDescription(Opts.UseInternalSslDescr, Opts.InterfacesGroup)]
        public bool UseInternalSsl { get; set; }
        [ArgDescription(Opts.SslTargetHostDescr, Opts.InterfacesGroup)]
        public string SslTargetHost { get; set; }
        [ArgDescription(Opts.SslValidateServerDescr, Opts.InterfacesGroup)]
        public bool SslValidateServer { get; set; }

        [ArgDescription(Opts.AuthenticationTypeDescr, Opts.AuthGroup)]
        public string AuthenticationType { get; set; }

        [ArgDescription(Opts.PrepareTimeoutMsDescr, Opts.DbGroup)]
        public int PrepareTimeoutMs { get; set; }
        [ArgDescription(Opts.CommitTimeoutMsDescr, Opts.DbGroup)]
        public int CommitTimeoutMs { get; set; }

        [ArgDescription(Opts.UnsafeDisableFlushToDiskDescr, Opts.DbGroup)]
        public bool UnsafeDisableFlushToDisk { get; set; }

        [ArgDescription(Opts.GossipIntervalMsDescr, Opts.ClusterGroup)]
        public int GossipIntervalMs { get; set; }
        [ArgDescription(Opts.GossipAllowedDifferenceMsDescr, Opts.ClusterGroup)]
        public int GossipAllowedDifferenceMs { get; set; }
        [ArgDescription(Opts.GossipTimeoutMsDescr, Opts.ClusterGroup)]
        public int GossipTimeoutMs { get; set; }

        public ClusterNodeOptions()
        {
            Config = "";
            Help = Opts.ShowHelpDefault;
            Version = Opts.ShowVersionDefault;
            Log = Opts.LogsDefault;
            Defines = Opts.DefinesDefault;
            WhatIf = Opts.WhatIfDefault;

            IntIp = Opts.InternalIpDefault;
            ExtIp = Opts.ExternalIpDefault;
            IntHttpPort = Opts.InternalHttpPortDefault;
            ExtHttpPort = Opts.ExternalHttpPortDefault;
            IntTcpPort = Opts.InternalTcpPortDefault;
            IntSecureTcpPort = Opts.InternalSecureTcpPortDefault;
            ExtTcpPort = Opts.ExternalTcpPortDefault;
            ExtSecureTcpPort = Opts.ExternalSecureTcpPortDefault;
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
            GossipSeed = Opts.GossipSeedDefault;

            StatsPeriodSec = Opts.StatsPeriodDefault;

            CachedChunks = Opts.CachedChunksDefault;
            ChunksCacheSize = Opts.ChunksCacheSizeDefault;

            Db = Opts.DbPathDefault;
            MemDb = Opts.InMemDbDefault;
            SkipDbVerify = Opts.SkipDbVerifyDefault;
            RunProjections = Opts.RunProjectionsDefault;
            ProjectionThreads = Opts.ProjectionThreadsDefault;
            WorkerThreads = Opts.WorkerThreadsDefault;

            HttpPrefixes = Opts.HttpPrefixesDefault;
            EnableTrustedAuth = Opts.EnableTrustedAuthDefault;

            ExtTcpHeartbeatTimeout = Opts.ExtTcpHeartbeatTimeoutDefault;
            IntTcpHeartbeatTimeout = Opts.IntTcpHeartbeatTimeoutDefault;

            ExtTcpHeartbeatInterval = Opts.ExtTcpHeartbeatIntervalDefault;
            IntTcpHeartbeatInterval = Opts.IntTcpHeartbeatInvervalDefault;


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
