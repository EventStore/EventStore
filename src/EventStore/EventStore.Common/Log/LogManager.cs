using System;
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

        public static void Init(string componentName, string logsDirectory)
        {
            Ensure.NotNull(componentName, "componentName");
            if (_initialized)
                throw new InvalidOperationException("Cannot initialize twice");

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
