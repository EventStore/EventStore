using System;

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
//                if(_instance == null)
//                {
//                    lock (InstanceLock)
//                    {
//                        if(_instance == null)
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
//            if(TryGet(key, args, out value))
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
//            if(_cmdValueProvider.TryGetValue(key, args, out value))
//                return true;
//            if(_configValueProvider.TryGetValue(key, args, out value))
//                return true;
//            if(_environmentValueProvider.TryGetValue(key, args, out value))
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

//    public interface IManagerKeys
//    {
//        SettingKey ExternalIp { get; }
//        SettingKey ExternalPort { get; }
//        SettingKey DNS { get; }
//        SettingKey FakeDNS { get; }
//        SettingKey NodesCount { get; }
//        SettingKey StatsUpdateInterval { get; }

//        /*
//         [Option("d", "cluster-dns")]
//         [Option(null, "nodes-count", Required = true)]
//         [Option("f", "fake-dns")]
//         [Option("p", "port", Required = true)]
//         [Option(null, "ip", Required = true)]
//         [Option("s", "stats-period-sec", DefaultValue = 30)]
//         */
//    }

//    public interface IClusterNodeKeys
//    {
//        SettingKey InternalIp { get; }
//        SettingKey ExternalIp { get; }

//        SettingKey InternalTcpPort { get; }
//        SettingKey ExternalTcpPort { get; }

//        SettingKey InternalHttpPort { get; }
//        SettingKey ExternalHttpPort { get; }

//        SettingKey ManagerIp { get; }
//        SettingKey ManagerPort { get; }

//        SettingKey DNS { get; }
//        SettingKey FakeDNS { get; }

//        SettingKey NodesCount { get; }
//        SettingKey CommitsCount { get; }
//        SettingKey PreparesCount { get; }
//        SettingKey ChunksToCache { get; }

//        SettingKey Db { get; }

//        SettingKey StatsUpdateInterval { get; }
//        /*
//        [Option(null, "int-ip", Required = true)]
//        [Option(null, "ext-ip", Required = true)]
//        [Option(null, "int-http-port", Required = true)]
//        [Option(null, "ext-http-port", Required = true)]
//        [Option(null, "int-tcp-port", Required = true)]
//        [Option(null, "ext-tcp-port", Required = true)]
//        [Option(null, "cluster-dns")]
//        [Option(null, "nodes-count", Required = true)]
//        [Option("f", "fake-dns")]
//        [Option(null, "manager-ip", Required = true)]
//        [Option(null, "manager-port", Required = true)]
//        [Option(null, "db")] // if null then db path is autogenerated
//        [Option(null, "commit-count", Required = true)]
//        [Option(null, "prepare-count", Required = true)]
//        [Option(null, "stats-period-sec", DefaultValue = 30)]
//        [Option(null, "chunkcache", DefaultValue = 2)]
//        */
//    }

//    public interface ISingleNodeKeys
//    {
//        SettingKey ExternalIp { get; }
//        SettingKey ExternalTcpPort { get; }
//        SettingKey ExternalHttpPort { get; }
//        SettingKey HttpPrefixes { get; }

//        SettingKey ChunksToCache { get; }
//        SettingKey Db { get; }

//        SettingKey ProjectionThreads { get; }

//        SettingKey NoProjections { get; }
//        SettingKey SkipHashesVerification { get; }

//        SettingKey StatsUpdateInterval { get; }

//        /*
//        [Option("t", "tcp-port", Required = true)]
//        [Option("h", "http-port", Required = true)]
//        [Option("i", "ip", Required = true)]
//        [Option("s", "stats-period-sec", DefaultValue = 30)]
//        [Option("c", "chunkcache", DefaultValue = 2)]
//        [Option(null, "db")]
//        [Option(null, "no-projections", DefaultValue = false)]
//        [Option(null, "do-not-verify-db-hashes-on-startup", DefaultValue = false)]
//        [Option(null, "projection-threads", DefaultValue = 3)]
//        [Option(null, "prefixes")]
//         */
//    }

//    public interface ITraceyKeys
//    {
//        SettingKey Db { get; }
//        SettingKey ChunksToCache { get; }
//        SettingKey Command { get; }

//        /*
//        [Option(null, "db", Required = true, HelpText = "Path to db")]
//        [Option(null, "chunkcache", DefaultValue = 2, HelpText = "Chunks to cache, 2 by default")]
//        [ValueList(typeof(List<string>), MaximumElements = -1)]
//         */
//    }

//    public interface ITestClientKeys
//    {
        
//    }

//    public interface IPadminKeys
//    {
        
//    }

//    public class SettingKey : IManagerKeys
//    {
//        public const string EnvPrfx = "ES_";

//        private static readonly SettingKey ClusterDns = new SettingKey("d", "cluster-dns", "clusterDns", EnvPrfx + "CLUSTERDNS");

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

//        SettingKey IManagerKeys.ExternalIp
//        {
//            get
//            {
//                throw new NotImplementedException();
//            }
//        }

//        SettingKey IManagerKeys.ExternalPort
//        {
//            get
//            {
//                throw new NotImplementedException();
//            }
//        }

//        SettingKey IManagerKeys.DNS
//        {
//            get
//            {
//                throw new NotImplementedException();
//            }
//        }

//        SettingKey IManagerKeys.FakeDNS
//        {
//            get
//            {
//                throw new NotImplementedException();
//            }
//        }

//        SettingKey IManagerKeys.NodesCount
//        {
//            get
//            {
//                throw new NotImplementedException();
//            }
//        }

//        SettingKey IManagerKeys.StatsUpdateInterval
//        {
//            get
//            {
//                throw new NotImplementedException();
//            }
//        }
//    }
//}
