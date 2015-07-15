using System;
using System.Linq;
using System.Reflection;
using System.IO;
using EventStore.Common.Utils;

namespace EventStore.Common.Log
{
    public static class LogManager
    {
        public static string LogsDirectory
        {
            get
            {
                if (!_initialized)
                    throw new InvalidOperationException("Init method must be called");
                return _logsDirectory;
            }
        }

        private const string EVENTSTORE_LOG_FILENAME = "log.config";
        private static readonly ILogger GlobalLogger = GetLogger("GLOBAL-LOGGER");
        private static bool _initialized;
        private static Func<string, ILogger> _logFactory = x => new NLogger(x);
        internal static string _logsDirectory;

        static LogManager()
        {
            var conf = NLog.Config.ConfigurationItemFactory.Default;
            conf.LayoutRenderers.RegisterDefinition("logsdir", typeof(NLogDirectoryLayoutRendered));
            conf.ConditionMethods.RegisterDefinition("is-dot-net", typeof(NLoggerHelperMethods).GetMethod("IsDotNet"));
            conf.ConditionMethods.RegisterDefinition("is-mono", typeof(NLoggerHelperMethods).GetMethod("IsMono"));
        }

        public static ILogger GetLoggerFor(Type type)
        {
            return GetLogger(type.Name);
        }

        public static ILogger GetLoggerFor<T>()
        {
            return GetLogger(typeof(T).Name);
        }

        public static ILogger GetLogger(string logName)
        {
            return new LazyLogger(() => _logFactory(logName));
        }

        public static void Init(string componentName, string logsDirectory, string configurationDirectory)
        {
            Ensure.NotNull(componentName, "componentName");
            if (_initialized)
                throw new InvalidOperationException("Cannot initialize twice");

            var potentialNLogConfigurationFilePaths = new []{
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), EVENTSTORE_LOG_FILENAME),
                Path.Combine(configurationDirectory, EVENTSTORE_LOG_FILENAME)
            }.Distinct();
            var configFilePath = potentialNLogConfigurationFilePaths.FirstOrDefault(x => File.Exists(x));
            if(!String.IsNullOrEmpty(configFilePath))
            {
                NLog.LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(configFilePath);
            }
            else
            {
                Console.Error.WriteLine("Event Store's Logging ({0}) configuration file was not found in:\n{1}.\nFalling back to NLog (NLog.config) defaults.", 
                        EVENTSTORE_LOG_FILENAME, 
                        String.Join(",\n", potentialNLogConfigurationFilePaths));
            }

            _initialized = true;

            _logsDirectory = logsDirectory;
            Environment.SetEnvironmentVariable("EVENTSTORE_INT-COMPONENT-NAME", componentName, EnvironmentVariableTarget.Process);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var exc = e.ExceptionObject as Exception;
                if (exc != null)
                    GlobalLogger.FatalException(exc, "Global Unhandled Exception occurred.");
                else
                    GlobalLogger.Fatal("Global Unhandled Exception object: {0}.", e.ExceptionObject);
                GlobalLogger.Flush(TimeSpan.FromMilliseconds(500));
            };
        }

        public static void Finish()
        {
            try
            {
                GlobalLogger.Flush();
                NLog.LogManager.Configuration = null;
            }
            catch (Exception exc)
            {
                GlobalLogger.ErrorException(exc, "Exception during flushing logs, ignoring...");
            }
        }

        public static void SetLogFactory(Func<string, ILogger> factory)
        {
            Ensure.NotNull(factory, "factory");
            _logFactory = factory;
        }
    }
}
