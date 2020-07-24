using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Exceptions;
using EventStore.Common.Log;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;
using EventStore.Rags;
using Microsoft.Extensions.Hosting;
using ILogger = Serilog.ILogger;

namespace EventStore.Core {
	public abstract class EventStoreHostedService<TOptions> : IHostedService where TOptions : class, IOptions, new() {
		// ReSharper disable StaticFieldInGenericType
		protected static readonly ILogger Log = Serilog.Log.ForContext<EventStoreHostedService<TOptions>>();
		// ReSharper restore StaticFieldInGenericType
		public bool SkipRun => _skipRun;
		private readonly bool _skipRun;
		public TOptions Options { get; }
		private string[] _args;

		protected EventStoreHostedService(string[] args) {
			try {
				_args = args;
				Options = LoadConfig();
				if (Options.Help) {
					Console.WriteLine("EventStoreDB version {0} ({1}/{2}, {3})",
						VersionInfo.Version, VersionInfo.Branch, VersionInfo.Hashtag, VersionInfo.Timestamp);
					Console.WriteLine();
					Console.WriteLine(EventStoreOptions.GetUsage<TOptions>());
					_skipRun = true;
				} else if (Options.Version) {
					Console.WriteLine("EventStoreDB version {0} ({1}/{2}, {3})",
						VersionInfo.Version, VersionInfo.Branch, VersionInfo.Hashtag, VersionInfo.Timestamp);
					_skipRun = true;
				} else {
					PreInit(Options);
					Init(Options);
					Create(Options);
				}
			} catch (OptionException exc) {
				Log.Error("Error while parsing options:");
				Log.Error(FormatExceptionMessage(exc));
				Log.Information("Options:");
				Log.Information(EventStoreOptions.GetUsage<TOptions>());
				_skipRun = true;
			} catch (InvalidConfigurationException exc) {
				Log.Error("Invalid Configuration Encountered");
				Log.Error(exc.Message);
				_skipRun = true;
			}
		}

		protected abstract string GetLogsDirectory(TOptions options);
		protected abstract string GetComponentName(TOptions options);

		protected abstract void Create(TOptions options);

		protected virtual IEnumerable<OptionSource>
			MutateEffectiveOptions(IEnumerable<OptionSource> effectiveOptions) =>
			effectiveOptions;

		public TOptions LoadConfig() => EventStoreOptions.Parse<TOptions>(_args, Opts.EnvPrefix,
			Path.Combine(Locations.DefaultConfigurationDirectory, DefaultFiles.DefaultConfigFile),
			MutateEffectiveOptions);

		protected abstract Task StartInternalAsync(CancellationToken cancellationToken);
		protected abstract Task StopInternalAsync(CancellationToken cancellationToken);

		protected virtual void PreInit(TOptions options) {
		}

		private void Init(TOptions options) {
			var projName = Assembly.GetEntryAssembly().GetName().Name.Replace(".", " - ");
			var componentName = GetComponentName(options);

			Console.Title = $"{projName}, {componentName}";

			string logsDirectory =
				Path.GetFullPath(options.Log.IsNotEmptyString() ? options.Log : GetLogsDirectory(options));
			EventStoreLoggerConfiguration.Initialize(logsDirectory, componentName);

			Log.Information("\n{description,-25} {version} ({branch}/{hashtag}, {timestamp})", "ES VERSION:",
				VersionInfo.Version, VersionInfo.Branch, VersionInfo.Hashtag, VersionInfo.Timestamp);
			Log.Information("{description,-25} {osFlavor} ({osVersion})", "OS:", OS.OsFlavor, Environment.OSVersion);
			Log.Information("{description,-25} {osRuntimeVersion} ({architecture}-bit)", "RUNTIME:",
				OS.GetRuntimeVersion(),
				Marshal.SizeOf(typeof(IntPtr)) * 8);
			Log.Information("{description,-25} {maxGeneration}", "GC:",
				GC.MaxGeneration == 0
					? "NON-GENERATION (PROBABLY BOEHM)"
					: $"{GC.MaxGeneration + 1} GENERATIONS");
			Log.Information("{description,-25} {logsDirectory}", "LOGS:", logsDirectory);

			Log.Information(EventStoreOptions.DumpOptions());
		}

		private string FormatExceptionMessage(Exception ex) {
			string msg = ex.Message;
			var exc = ex.InnerException;
			int cnt = 0;
			while (exc != null) {
				cnt += 1;
				msg += "\n" + new string(' ', 2 * cnt) + exc.Message;
				exc = exc.InnerException;
			}

			return msg;
		}

		Task IHostedService.StartAsync(CancellationToken cancellationToken) =>
			_skipRun ? Task.CompletedTask : StartInternalAsync(cancellationToken);

		Task IHostedService.StopAsync(CancellationToken cancellationToken) => StopInternalAsync(cancellationToken);
	}
}
