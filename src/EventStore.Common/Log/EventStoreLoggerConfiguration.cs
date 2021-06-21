using System;
using System.IO;
using System.Linq;
using System.Threading;
using EventStore.Common.Exceptions;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Filters;

namespace EventStore.Common.Log {
	public class EventStoreLoggerConfiguration {
		private const string ConsoleOutputTemplate =
			"[{ProcessId,5},{ThreadId,2},{Timestamp:HH:mm:ss.fff},{Level:u3}] {Message}{NewLine}{Exception}";

		public static readonly Logger ConsoleLog = StandardLoggerConfiguration
			.WriteTo.Console(outputTemplate: ConsoleOutputTemplate)
			.CreateLogger();

		private static readonly Func<LogEvent, bool> RegularStats = Matching.FromSource("REGULAR-STATS-LOGGER");

		private static int Initialized;
		private static LoggingLevelSwitch _defaultLogLevelSwitch;
		private static object _defaultLogLevelSwitchLock = new object();

		private readonly string _logsDirectory;
		private readonly string _componentName;
		private readonly LoggerConfiguration _loggerConfiguration;

		static EventStoreLoggerConfiguration() {
			Serilog.Log.Logger = ConsoleLog;
			AppDomain.CurrentDomain.UnhandledException += (s, e) => {
				if (e.ExceptionObject is Exception exc)
					Serilog.Log.Fatal(exc, "Global Unhandled Exception occurred.");
				else
					Serilog.Log.Fatal("Global Unhandled Exception object: {e}.", e.ExceptionObject);
			};
		}

		public static void Initialize(string logsDirectory, string componentName, string logConfig = "logconfig.json") {
			if (Interlocked.Exchange(ref Initialized, 1) == 1) {
				throw new InvalidOperationException($"{nameof(Initialize)} may not be called more than once.");
			}

			if (logsDirectory.StartsWith("~")) {
				throw new ApplicationInitializationException(
					"The given log path starts with a '~'. Event Store does not expand '~'.");
			}

			var logConfigurationDirectory = Path.IsPathRooted(logConfig)
				? Path.GetDirectoryName(logConfig)
				: Locations
					.GetPotentialConfigurationDirectories()
					.FirstOrDefault(directory => File.Exists(Path.Combine(directory, logConfig)));

			if (logConfigurationDirectory == null) {
				throw new FileNotFoundException(
					$"Could not find {logConfig} in the following directories: {string.Join(", ", Locations.GetPotentialConfigurationDirectories())}");
			}

			var configurationRoot = new ConfigurationBuilder()
				.AddJsonFile(config => {
					config.Optional = false;
					config.FileProvider = new PhysicalFileProvider(logConfigurationDirectory) {
						UseActivePolling = true,
						UsePollingFileWatcher = true
					};
					config.OnLoadException = context => Serilog.Log.Error(context.Exception, "err");
					config.Path = Path.GetFileName(logConfig);
					config.ReloadOnChange = true;
				})
				.Build();

			Serilog.Debugging.SelfLog.Enable(ConsoleLog.Information);

			Serilog.Log.Logger = (configurationRoot.GetSection("Serilog").Exists()
					? FromConfiguration(configurationRoot)
					: Default(logsDirectory, componentName, configurationRoot))
				.CreateLogger();

			Serilog.Debugging.SelfLog.Disable();
		}

		public static bool AdjustMinimumLogLevel(LogLevel logLevel) {
			lock (_defaultLogLevelSwitchLock) {
				if (_defaultLogLevelSwitch == null) {
					throw new InvalidOperationException($"The logger configuration has not yet been initialized.");
				}

				if (Enum.TryParse<LogEventLevel>(logLevel.ToString(), out var serilogLogLevel)) {
					if (serilogLogLevel != _defaultLogLevelSwitch.MinimumLevel) {
						_defaultLogLevelSwitch.MinimumLevel = serilogLogLevel;
						return true;
					}

					return false;
				} else {
					throw new ArgumentException($"'{logLevel}' is not a valid log level.");
				}
			}
		}

		private static LoggerConfiguration FromConfiguration(IConfiguration configuration) =>
			new LoggerConfiguration().ReadFrom.Configuration(configuration);

		private static LoggerConfiguration Default(string logsDirectory, string componentName,
			IConfigurationRoot logLevelConfigurationRoot) =>
			new EventStoreLoggerConfiguration(logsDirectory, componentName, logLevelConfigurationRoot);

		private EventStoreLoggerConfiguration(string logsDirectory, string componentName,
			IConfigurationRoot logLevelConfigurationRoot) {
			if (logsDirectory == null) {
				throw new ArgumentNullException(nameof(logsDirectory));
			}

			if (componentName == null) {
				throw new ArgumentNullException(nameof(componentName));
			}

			if (logLevelConfigurationRoot == null) {
				throw new ArgumentNullException(nameof(logLevelConfigurationRoot));
			}

			_logsDirectory = logsDirectory;
			_componentName = componentName;

			var loglevelSection = logLevelConfigurationRoot.GetSection("Logging").GetSection("LogLevel");
			var defaultLogLevelSection = loglevelSection.GetSection("Default");
			lock (_defaultLogLevelSwitchLock) {
				_defaultLogLevelSwitch = new LoggingLevelSwitch {
					MinimumLevel = LogEventLevel.Verbose
				};
				ApplyLogLevel(defaultLogLevelSection, _defaultLogLevelSwitch);
			}

			var loggerConfiguration = StandardLoggerConfiguration
				.MinimumLevel.ControlledBy(_defaultLogLevelSwitch)
				.WriteTo.Async(AsyncSink);

			foreach (var namedLogLevelSection in loglevelSection.GetChildren().Where(x => x.Key != "Default")) {
				var levelSwitch = new LoggingLevelSwitch();
				ApplyLogLevel(namedLogLevelSection, levelSwitch);
				loggerConfiguration = loggerConfiguration.MinimumLevel.Override(namedLogLevelSection.Key, levelSwitch);
			}

			_loggerConfiguration = loggerConfiguration;

			void ApplyLogLevel(IConfigurationSection namedLogLevelSection, LoggingLevelSwitch levelSwitch) {
				TrySetLogLevel(namedLogLevelSection, levelSwitch);
				ChangeToken.OnChange(namedLogLevelSection.GetReloadToken,
					() => TrySetLogLevel(namedLogLevelSection, levelSwitch));
			}

			static void TrySetLogLevel(IConfigurationSection logLevel, LoggingLevelSwitch levelSwitch) {
				if (!Enum.TryParse<LogEventLevel>(logLevel.Value, out var level)) {
					return;
				}

				levelSwitch.MinimumLevel = level;
			}
		}

		private static LoggerConfiguration StandardLoggerConfiguration =>
			new LoggerConfiguration()
				.Enrich.WithProcessId()
				.Enrich.WithThreadId()
				.Enrich.FromLogContext();

		private void AsyncSink(LoggerSinkConfiguration configuration) {
			configuration.Logger(c => c
				.Filter.ByIncludingOnly(RegularStats)
				.WriteTo.Logger(Stats));
			configuration.Logger(c => c
				.Filter.ByExcluding(RegularStats)
				.WriteTo.Logger(Default));
		}

		private void Default(LoggerConfiguration configuration) =>
			configuration
				.WriteTo.Console(outputTemplate: ConsoleOutputTemplate)
				.WriteTo.RollingFile(GetLogFileName())
				.WriteTo.Logger(Error);

		private void Error(LoggerConfiguration configuration) =>
			configuration
				.Filter.ByIncludingOnly(Errors)
				.WriteTo.RollingFile(GetLogFileName("err"));

		private void Stats(LoggerConfiguration configuration) =>
			configuration.WriteTo.RollingFile(GetLogFileName("stats"));

		private string GetLogFileName(string log = null) =>
			Path.Combine(_logsDirectory, $"{_componentName}/log{(log == null ? string.Empty : $"-{log}")}.json");

		private static bool Errors(LogEvent e) => e.Exception != null || e.Level >= LogEventLevel.Error;

		public static implicit operator LoggerConfiguration(EventStoreLoggerConfiguration configuration) =>
			configuration._loggerConfiguration;
	}
}
