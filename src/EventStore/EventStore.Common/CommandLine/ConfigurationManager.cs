//using System;

//namespace EventStore.Common.CommandLine
//{
//    public class ConfigurationManager
//    {
//        private static volatile ConfigurationManager _instance;
//        private static readonly object InstanceLock = new object();

//        public static ConfigurationManager Instance
//        {
//            get
//            {
//                if (_instance == null)
//                {
//                    lock (InstanceLock)
//                    {
//                        if (_instance == null)
//                        {
//                            _instance = new ConfigurationManager();
//                        }
//                    }
//                }

//                return _instance;
//            }
//        }

//        private readonly IValueProvider _cmdValueProvider;
//        private readonly IValueProvider _configValueProvider;
//        private readonly IValueProvider _environmentValueProvider;

//        internal ConfigurationManager(IValueProvider cmdValueProvider,
//                                      IValueProvider configValueProvider,
//                                      IValueProvider environmentValueProvider)
//        {
//            _cmdValueProvider = cmdValueProvider;
//            _configValueProvider = configValueProvider;
//            _environmentValueProvider = environmentValueProvider;
//        }

//        private ConfigurationManager()
//            : this(new CmdValueProvider(), new ConfigValueProvider(), new EnvironmentValueProvider())
//        {
//        }

//        public T GetValue<T>(SettingKey key, string[] args)
//        {
//            T value;
//            if (TryGet(key, args, out value))
//                return value;

//            throw new Exception(string.Format("Cannot load value for {0} from cmdline, config or environment", key));
//        }

//        public T GetValueOr<T>(SettingKey key, string[] args, T fallbackValue)
//        {
//            T value;
//            return TryGet(key, args, out value) ? value : fallbackValue;
//        }

//        public T GetValueOrTypeDefault<T>(SettingKey key, string[] args)
//        {
//            T value;
//            return TryGet(key, args, out value) ? value : default(T);
//        }

//        private bool TryGet<T>(SettingKey key, string[] args, out T value)
//        {
//            if (_cmdValueProvider.TryGetValue(key, args, out value))
//                return true;
//            if (_configValueProvider.TryGetValue(key, args, out value))
//                return true;
//            if (_environmentValueProvider.TryGetValue(key, args, out value))
//                return true;

//            return false;
//        }
//    }

//    public interface IValueProvider
//    {
//        bool TryGetValue<T>(SettingKey key, string[] args, out T value);
//    }

//    public class CmdValueProvider : IValueProvider
//    {
//        public bool TryGetValue<T>(SettingKey key, string[] args, out T value)
//        {
//            throw new NotImplementedException();
//        }
//    }

//    public class ConfigValueProvider : IValueProvider
//    {
//        public bool TryGetValue<T>(SettingKey key, string[] args, out T value)
//        {
//            throw new NotImplementedException();
//        }
//    }

//    public class EnvironmentValueProvider : IValueProvider
//    {
//        public bool TryGetValue<T>(SettingKey key, string[] args, out T value)
//        {
//            throw new NotImplementedException();
//        }
//    }

//    public interface ISingleNodeKeys
//    {
//        SettingKey ExternalIp { get; }

//        SettingKey ExternalTcpPort { get; }
//        SettingKey ExternalHttpPort { get; }

//        SettingKey HttpPrefixes { get; }

//        SettingKey DB { get; }
//        SettingKey ChunksToCache { get; }

//        SettingKey ProjectionThreads { get; }

//        SettingKey NoProjections { get; }
//        SettingKey SkipHashesVerification { get; }

//        SettingKey StatsUpdateInterval { get; }
//    }

//    public interface IManagerKeys
//    {
//        SettingKey ExternalIp { get; }
//        SettingKey ExternalHttpPort { get; }

//        SettingKey DNS { get; }
//        SettingKey FakeDNS { get; }

//        SettingKey Watchdog { get; }

//        SettingKey NodesCount { get; }
//        SettingKey StatsUpdateInterval { get; }
//    }

//    public interface IClusterNodeKeys
//    {
//        SettingKey InternalIp { get; }
//        SettingKey ExternalIp { get; }

//        SettingKey InternalTcpPort { get; }
//        SettingKey ExternalTcpPort { get; }

//        SettingKey InternalHttpPort { get; }
//        SettingKey ExternalHttpPort { get; }

//        SettingKey ExternalManagerIp { get; }
//        SettingKey ExternalManagerHttpPort { get; }

//        SettingKey DNS { get; }
//        SettingKey FakeDNS { get; }

//        SettingKey DB { get; }
//        SettingKey ChunksToCache { get; }

//        SettingKey NodesCount { get; }

//        SettingKey CommitsCount { get; }
//        SettingKey PreparesCount { get; }

//        SettingKey StatsUpdateInterval { get; }
//    }

//    public interface ITraceyKeys
//    {
//    }

//    public interface ITestClientKeys
//    {
//    }

//    public interface IPadminKeys
//    {

//    }

//    public class SettingKey : ISingleNodeKeys,
//                              IManagerKeys,
//                              IClusterNodeKeys
//    {
//        public const string EnvPrfx = "ES_";

//        internal static readonly SettingKey InternalIp = new SettingKey("", "internal-ip", "internalIp", null);
//        internal static readonly SettingKey ExternalIp = new SettingKey("", "external-ip", "externalIp", null);
//        internal static readonly SettingKey ExternalManagerIp = new SettingKey("", "external-manager-ip", "externalManagerIp", EnvPrfx + "EXTERNALMANAGERIP");

//        internal static readonly SettingKey InternalTcpPort = new SettingKey("", "internal-tcp-port", "internalTcpPort", null);
//        internal static readonly SettingKey ExternalTcpPort = new SettingKey("", "external-tcp-port", "externalTcpPort", null);
//        internal static readonly SettingKey InternalHttpPort = new SettingKey("", "internal-http-port", "internalHttpPort", null);
//        internal static readonly SettingKey ExternalHttpPort = new SettingKey("", "external-http-port", "externalHttpPort", null);
//        internal static readonly SettingKey ExternalManagerHttpPort = new SettingKey("", "external-manager-http-port", "externalManagerHttpPort", EnvPrfx + "EXTERNALMANAGERHTTPPORT");

//        private static readonly SettingKey HttpPrefixes = new SettingKey("", "http-prefixes", "httpPrefixes", EnvPrfx + "HTTPPREFIXES");

//        private static readonly SettingKey DNS = new SettingKey("", "cluster-dns", "clusterDns", EnvPrfx + "CLUSTERDNS");
//        private static readonly SettingKey FakeDNS = new SettingKey("", "fake-dns", "fakeDns", EnvPrfx + "FAKEDNS");

//        private static readonly SettingKey DB = new SettingKey("", "db", "db", EnvPrfx + "DB");
//        private static readonly SettingKey ChunksToCache = new SettingKey("", "chunks-to-cache", "chunksToCache", EnvPrfx + "CHUNKSTOCACHE");

//        private static readonly SettingKey NodesCount = new SettingKey("", "nodes-count", "nodesCount", EnvPrfx + "NODESCOUNT");

//        private static readonly SettingKey CommitsCount = new SettingKey("", "commits-count", "commitsCount", EnvPrfx + "COMMITSCOUNT");
//        private static readonly SettingKey PreparesCount = new SettingKey("", "prepares-count", "preparesCount", EnvPrfx + "PREPARESCOUNT");

//        private static readonly SettingKey ProjectionThreads = new SettingKey("", "projection-threads", "projectionThreads", EnvPrfx + "PROJECTIONTHREADS");
//        private static readonly SettingKey Watchdog = new SettingKey("", "watchdog", "watchdog", EnvPrfx + "WATCHDOG");

//        private static readonly SettingKey NoProjections = new SettingKey("", "NoProjections", "noProjections", EnvPrfx + "NOPROJECTIONS");
//        private static readonly SettingKey SkipHashesVerification = new SettingKey("", "skip-hashes-verification", "skipHashesVerification", EnvPrfx + "SKIPHASHESVERIFICATION");

//        private static readonly SettingKey StatsUpdateInterval = new SettingKey("", "stats-update-interval", "statsUpdateInterval", EnvPrfx + "STATSUPDATEINTERVAL");

//        public IManagerKeys Manager
//        {
//            get
//            {
//                return this;
//            }
//        }

//        public ISingleNodeKeys SingleNode
//        {
//            get
//            {
//                return this;
//            }
//        }

//        public IClusterNodeKeys ClusterNode
//        {
//            get
//            {
//                return this;
//            }
//        }

//        #region manager

//        SettingKey IManagerKeys.ExternalIp
//        {
//            get
//            {
//                return ExternalIp;
//            }
//        }

//        SettingKey IManagerKeys.ExternalHttpPort
//        {
//            get
//            {
//                return ExternalHttpPort;
//            }
//        }

//        SettingKey IManagerKeys.DNS
//        {
//            get
//            {
//                return DNS;
//            }
//        }

//        SettingKey IManagerKeys.FakeDNS
//        {
//            get
//            {
//                return FakeDNS;
//            }
//        }

//        SettingKey IManagerKeys.Watchdog
//        {
//            get
//            {
//                return Watchdog;
//            }
//        }

//        SettingKey IManagerKeys.NodesCount
//        {
//            get
//            {
//                return NodesCount;
//            }
//        }

//        SettingKey IManagerKeys.StatsUpdateInterval
//        {
//            get
//            {
//                return StatsUpdateInterval;
//            }
//        }

//        #endregion

//        #region singlenode

//        SettingKey ISingleNodeKeys.ExternalIp
//        {
//            get
//            {
//                return ExternalIp;
//            }
//        }

//        SettingKey ISingleNodeKeys.ExternalTcpPort
//        {
//            get
//            {
//                return ExternalTcpPort;
//            }
//        }

//        SettingKey ISingleNodeKeys.ExternalHttpPort
//        {
//            get
//            {
//                return ExternalHttpPort;
//            }
//        }

//        SettingKey ISingleNodeKeys.HttpPrefixes
//        {
//            get
//            {
//                return HttpPrefixes;
//            }
//        }

//        SettingKey ISingleNodeKeys.DB
//        {
//            get
//            {
//                return DB;
//            }
//        }

//        SettingKey ISingleNodeKeys.ChunksToCache
//        {
//            get
//            {
//                return ChunksToCache;
//            }
//        }

//        SettingKey ISingleNodeKeys.ProjectionThreads
//        {
//            get
//            {
//                return ProjectionThreads;
//            }
//        }

//        SettingKey ISingleNodeKeys.NoProjections
//        {
//            get
//            {
//                return NoProjections;
//            }
//        }

//        SettingKey ISingleNodeKeys.SkipHashesVerification
//        {
//            get
//            {
//                return SkipHashesVerification;
//            }
//        }

//        SettingKey ISingleNodeKeys.StatsUpdateInterval
//        {
//            get
//            {
//                return StatsUpdateInterval;
//            }
//        }

//        #endregion

//        #region clusternode

//        SettingKey IClusterNodeKeys.InternalIp
//        {
//            get
//            {
//                return InternalIp;
//            }
//        }

//        SettingKey IClusterNodeKeys.ExternalIp
//        {
//            get
//            {
//                return ExternalIp;
//            }
//        }

//        SettingKey IClusterNodeKeys.InternalTcpPort
//        {
//            get
//            {
//                return InternalTcpPort;
//            }
//        }

//        SettingKey IClusterNodeKeys.ExternalTcpPort
//        {
//            get
//            {
//                return ExternalTcpPort;
//            }
//        }

//        SettingKey IClusterNodeKeys.InternalHttpPort
//        {
//            get
//            {
//                return InternalHttpPort;
//            }
//        }

//        SettingKey IClusterNodeKeys.ExternalHttpPort
//        {
//            get
//            {
//                return ExternalHttpPort;
//            }
//        }

//        SettingKey IClusterNodeKeys.ExternalManagerIp
//        {
//            get
//            {
//                return ExternalManagerIp;
//            }
//        }

//        SettingKey IClusterNodeKeys.ExternalManagerHttpPort
//        {
//            get
//            {
//                return ExternalManagerHttpPort;
//            }
//        }

//        SettingKey IClusterNodeKeys.DNS
//        {
//            get
//            {
//                return DNS;
//            }
//        }

//        SettingKey IClusterNodeKeys.FakeDNS
//        {
//            get
//            {
//                return FakeDNS;
//            }
//        }

//        SettingKey IClusterNodeKeys.DB
//        {
//            get
//            {
//                return DB;
//            }
//        }

//        SettingKey IClusterNodeKeys.ChunksToCache
//        {
//            get
//            {
//                return ChunksToCache;
//            }
//        }

//        SettingKey IClusterNodeKeys.NodesCount
//        {
//            get
//            {
//                return NodesCount;
//            }
//        }

//        SettingKey IClusterNodeKeys.CommitsCount
//        {
//            get
//            {
//                return CommitsCount;
//            }
//        }

//        SettingKey IClusterNodeKeys.PreparesCount
//        {
//            get
//            {
//                return PreparesCount;
//            }
//        }

//        SettingKey IClusterNodeKeys.StatsUpdateInterval
//        {
//            get
//            {
//                return StatsUpdateInterval;
//            }
//        }

//        #endregion

//        internal readonly string CmdShort;
//        internal readonly string CmdLong;

//        internal readonly string Config;
//        internal readonly string Env;

//        private SettingKey(string cmdShort, string cmdLong, string config, string env)
//        {
//            CmdShort = cmdShort;
//            CmdLong = cmdLong;

//            Config = config;
//            Env = env;
//        }
//    }
//}
