using System;
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

namespace EventStore.Core {
	public abstract class ProgramBase<TOptions> where TOptions : class, IOptions, new() {
		// ReSharper disable StaticFieldInGenericType
		protected static readonly ILogger Log = LogManager.GetLoggerFor<ProgramBase<TOptions>>();
		// ReSharper restore StaticFieldInGenericType

		private int _exitCode;
		private readonly ManualResetEventSlim _exitEvent = new ManualResetEventSlim(false);
		private readonly TaskCompletionSource<int> _exitSource = new TaskCompletionSource<int>();

		protected abstract string GetLogsDirectory(TOptions options);
		protected abstract bool GetIsStructuredLog(TOptions options);
		protected abstract string GetComponentName(TOptions options);

		protected abstract void Create(TOptions options);
		protected abstract void Start();
		public abstract void Stop();

		protected ProgramBase(string[] args) {
			Application.RegisterExitAction(Exit);
			try {

				var options = EventStoreOptions.Parse<TOptions>(args, Opts.EnvPrefix,
					Path.Combine(Locations.DefaultConfigurationDirectory, DefaultFiles.DefaultConfigFile));
				if (options.Help) {
					Console.WriteLine("Options:");
					Console.WriteLine(EventStoreOptions.GetUsage<TOptions>());
				} else if (options.Version) {
					Console.WriteLine("EventStore version {0} ({1}/{2}, {3})",
						VersionInfo.Version, VersionInfo.Branch, VersionInfo.Hashtag, VersionInfo.Timestamp);
					return 0;
				} else {
					PreInit(options);
					Init(options);
					Create(options);
					Start();
				}
			} catch (OptionException exc) {
				Console.Error.WriteLine("Error while parsing options:");
				Console.Error.WriteLine(FormatExceptionMessage(exc));
				Console.Error.WriteLine();
				Console.Error.WriteLine("Options:");
				Console.Error.WriteLine(EventStoreOptions.GetUsage<TOptions>());
				return 1;
			} catch (ApplicationInitializationException ex) {
				var msg = String.Format("Application initialization error: {0}", FormatExceptionMessage(ex));
				if (LogManager.Initialized) {
					Log.FatalException(ex, msg);
				} else {
					Console.Error.WriteLine(msg);
				}

				return 1;
			} catch (Exception ex) {
				var msg = "Unhandled exception while starting application:";
				if (LogManager.Initialized) {
					Log.FatalException(ex, msg);
					Log.FatalException(ex, "{e}", FormatExceptionMessage(ex));
				} else {
					Console.Error.WriteLine(msg);
					Console.Error.WriteLine(FormatExceptionMessage(ex));
				}

				return 1;
			} finally {
				Log.Flush();
			}
		}

		public async Task<int> Run() {
			try {
				await Task.WhenAny(_startupSource.Task, Start());
				var exitCode = await _exitSource.Task;
				await Stop();

				return exitCode;
			} catch (Exception ex) {
				if (LogManager.Initialized) {
					Log.FatalException(ex, "{e}", FormatExceptionMessage(ex));
				} else {
					Console.Error.WriteLine(ex.Message);
					Console.Error.WriteLine(FormatExceptionMessage(ex));
				}
				
				return 1;
			}
		}

		protected virtual void PreInit(TOptions options) {
		}

		private void Exit(int exitCode) {
			LogManager.Finish();

			_exitSource.TrySetResult(exitCode);
		}

		protected virtual void OnProgramExit() {
		}

		private void Init(TOptions options) {
			Application.AddDefines(options.Defines);

			var projName = Assembly.GetEntryAssembly().GetName().Name.Replace(".", " - ");
			var componentName = GetComponentName(options);

			Console.Title = string.Format("{0}, {1}", projName, componentName);

			string logsDirectory =
				Path.GetFullPath(options.Log.IsNotEmptyString() ? options.Log : GetLogsDirectory(options));
			bool structuredLog = GetIsStructuredLog(options);

			LogManager.Init(componentName, logsDirectory, structuredLog, Locations.DefaultConfigurationDirectory);

			Log.Info("\n{description,-25} {version} ({branch}/{hashtag}, {timestamp})", "ES VERSION:",
				VersionInfo.Version, VersionInfo.Branch, VersionInfo.Hashtag, VersionInfo.Timestamp);
			Log.Info("{description,-25} {osFlavor} ({osVersion})", "OS:", OS.OsFlavor, Environment.OSVersion);
			Log.Info("{description,-25} {osRuntimeVersion} ({architecture}-bit)", "RUNTIME:", OS.GetRuntimeVersion(),
				Marshal.SizeOf(typeof(IntPtr)) * 8);
			Log.Info("{description,-25} {maxGeneration}", "GC:",
				GC.MaxGeneration == 0
					? "NON-GENERATION (PROBABLY BOEHM)"
					: string.Format("{0} GENERATIONS", GC.MaxGeneration + 1));
			Log.Info("{description,-25} {logsDirectory}", "LOGS:", LogManager.LogsDirectory);


			if (!structuredLog)
				Log.Info("{esOptions}", EventStoreOptions.DumpOptions());
			else {
				Console.WriteLine(EventStoreOptions.DumpOptions());
				Log.Info("{@esOptions}", EventStoreOptions.DumpOptionsStructured());
			}

			if (options.WhatIf)
				Application.Exit(ExitCode.Success, "WhatIf option specified");
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

		protected static StoreLocation GetCertificateStoreLocation(string certificateStoreLocation) {
			StoreLocation location;
			if (!Enum.TryParse(certificateStoreLocation, out location))
				throw new Exception(string.Format("Could not find certificate store location '{0}'",
					certificateStoreLocation));
			return location;
		}

		protected static StoreName GetCertificateStoreName(string certificateStoreName) {
			StoreName name;
			if (!Enum.TryParse(certificateStoreName, out name))
				throw new Exception(string.Format("Could not find certificate store name '{0}'", certificateStoreName));
			return name;
		}
	}
}
