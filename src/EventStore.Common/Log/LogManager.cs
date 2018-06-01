using System;
using System.Linq;
using System.IO;
using EventStore.Common.Utils;
using Serilog;


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

        public static bool Initialized
        {
            get
            {
                return _initialized;
            }
        }

        private const string EVENTSTORE_LOG_FILENAME = "log.config";
        private static readonly ILogger GlobalLogger = GetLogger("GLOBAL-LOGGER");
        private static bool _initialized;
        private static Func<string, ILogger> _logFactory = x => new SeriLogger(x);
        internal static string _logsDirectory;

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

            var potentialSeriLogConfigurationFilePaths = new []{
                Path.Combine(Locations.ApplicationDirectory, EVENTSTORE_LOG_FILENAME),
                Path.Combine(configurationDirectory, EVENTSTORE_LOG_FILENAME)
            }.Distinct();

             _logsDirectory = logsDirectory;
            
            Environment.SetEnvironmentVariable("EVENTSTORE_INT-COMPONENT-NAME", componentName, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("logsdir", _logsDirectory, EnvironmentVariableTarget.Process);

            var configFilePath = potentialSeriLogConfigurationFilePaths.FirstOrDefault(x => File.Exists(x));
            if(!String.IsNullOrEmpty(configFilePath))
            {
                 Serilog.Log.Logger =
                 new Serilog.LoggerConfiguration()
                 .MinimumLevel.Verbose()
                 .WriteTo.Logger( lc => lc.ReadFrom.AppSettings("std",configFilePath))
                 .WriteTo.Logger( lc => lc.ReadFrom.AppSettings("error",configFilePath))
                 .WriteTo.Logger( lc => lc.ReadFrom.AppSettings("stats",configFilePath))
                 .CreateLogger();
            }
            else
            {
                Console.Error.WriteLine("Event Store's Logging ({0}) configuration file was not found in:\n{1}.\nFalling back to defaults.",
                        EVENTSTORE_LOG_FILENAME,
                        String.Join(",\n", potentialSeriLogConfigurationFilePaths));
            }

            _initialized = true;


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
            }
            catch (Exception exc)
            {
                GlobalLogger.ErrorException(exc, "Exception while flushing logs, ignoring...");
            }
        }

        public static void SetLogFactory(Func<string, ILogger> factory)
        {
            Ensure.NotNull(factory, "factory");
            _logFactory = factory;
        }
    }
}
