using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using EventStore.Common.Exceptions;
using EventStore.Common.Log;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.Util;
using EventStore.Rags;

namespace EventStore.Core
{
    public abstract class ProgramBase<TOptions> where TOptions : class, IOptions, new()
    {
// ReSharper disable StaticFieldInGenericType
        protected static readonly ILogger Log = LogManager.GetLoggerFor<ProgramBase<TOptions>>();
// ReSharper restore StaticFieldInGenericType

        private int _exitCode;
        private readonly ManualResetEventSlim _exitEvent = new ManualResetEventSlim(false);

        protected abstract string GetLogsDirectory(TOptions options);
        protected abstract string GetComponentName(TOptions options);

        protected abstract void Create(TOptions options);
        protected abstract void Start();
        public abstract void Stop();

        public int Run(string[] args)
        {
            try
            {
                Application.RegisterExitAction(Exit);

                var options = EventStoreOptions.Parse<TOptions>(args, Opts.EnvPrefix, Path.Combine(Locations.DefaultConfigurationDirectory, DefaultFiles.DefaultConfigFile));
                if (options.Help)
                {
                    Console.WriteLine("Options:");
                    Console.WriteLine(EventStoreOptions.GetUsage<TOptions>());
                }
                else if (options.Version)
                {
                    Console.WriteLine("EventStore version {0} ({1}/{2}, {3})",
                                      VersionInfo.Version, VersionInfo.Branch, VersionInfo.Hashtag, VersionInfo.Timestamp);
                    Application.ExitSilent(0, "Normal exit.");
                }
                else
                {
                    PreInit(options);
                    Init(options);
                    CommitSuicideIfInBoehmOrOnBadVersionsOfMono(options);
                    Create(options);
                    Start();

                    _exitEvent.Wait();
                }
            }
            catch (OptionException exc)
            {
                Console.Error.WriteLine("Error while parsing options:");
                Console.Error.WriteLine(FormatExceptionMessage(exc));
                Console.Error.WriteLine();
                Console.Error.WriteLine("Options:");
                Console.Error.WriteLine(EventStoreOptions.GetUsage<TOptions>());
            }
            catch (ApplicationInitializationException ex)
            {
                Log.FatalException(ex, "Application initialization error: {0}", FormatExceptionMessage(ex));
                Application.Exit(ExitCode.Error, FormatExceptionMessage(ex));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Log.FatalException(ex, "Unhandled exception while starting application:");
                Log.FatalException(ex, "{0}", FormatExceptionMessage(ex));
                Application.Exit(ExitCode.Error, FormatExceptionMessage(ex));
            }
            finally
            {
                Log.Flush();
            }

            Application.ExitSilent(_exitCode, "Normal exit.");
            return _exitCode;
        }

        protected virtual void PreInit(TOptions options)
        {
        }

        private void CommitSuicideIfInBoehmOrOnBadVersionsOfMono(TOptions options)
        {
            if(!options.Force)
            {
                if(GC.MaxGeneration == 0)
                {
                    Application.Exit(3, "Appears that we are running in mono with boehm GC this is generally not a good idea, please run with sgen instead." + 
                        "to run with sgen use mono --gc=sgen. If you really want to run with boehm GC you can use --force to override this error.");
                }
                if(OS.IsUnix && !OS.GetRuntimeVersion().StartsWith("3"))
                {
                    Log.Warn("You appear to be running a version of Mono which is untested and unsupported. Only Mono 3 is supported at this time.");
                }
            }
        }

        private void Exit(int exitCode)
        {
            LogManager.Finish();

            _exitCode = exitCode;
            _exitEvent.Set();
        }

        protected virtual void OnProgramExit()
        {
        }

        private void Init(TOptions options)
        {
            Application.AddDefines(options.Defines);

            var projName = Assembly.GetEntryAssembly().GetName().Name.Replace(".", " - ");
            var componentName = GetComponentName(options);

            Console.Title = string.Format("{0}, {1}", projName, componentName);

            string logsDirectory = Path.GetFullPath(options.Log.IsNotEmptyString() ? options.Log : GetLogsDirectory(options));
            LogManager.Init(componentName, logsDirectory, Locations.DefaultConfigurationDirectory);

            Log.Info("\n{0,-25} {1} ({2}/{3}, {4})", "ES VERSION:", VersionInfo.Version, VersionInfo.Branch, VersionInfo.Hashtag, VersionInfo.Timestamp);
            Log.Info("{0,-25} {1} ({2})", "OS:", OS.OsFlavor, Environment.OSVersion);
            Log.Info("{0,-25} {1} ({2}-bit)", "RUNTIME:", OS.GetRuntimeVersion(), Marshal.SizeOf(typeof(IntPtr)) * 8);
            Log.Info("{0,-25} {1} ({2} max)", "GC:", GC.MaxGeneration == 0 ? "NON-GENERATION (PROBABLY BOEHM)" : string.Format("{0} GENERATIONS", GC.MaxGeneration + 1));
            Log.Info("{0,-25} {1}", "LOGS:", LogManager.LogsDirectory);
            Log.Info("{0}", EventStoreOptions.DumpOptions());

            if (options.WhatIf)
                Application.Exit(ExitCode.Success, "WhatIf option specified");
        }

        private string FormatExceptionMessage(Exception ex)
        {
            string msg = ex.Message;
            var exc = ex.InnerException;
            int cnt = 0;
            while (exc != null)
            {
                cnt += 1;
                msg += "\n" + new string(' ', 2 * cnt) + exc.Message;
                exc = exc.InnerException;
            }
            return msg;
        }

        protected static StoreLocation GetCertificateStoreLocation(string certificateStoreLocation)
        {
            StoreLocation location;
            if (!Enum.TryParse(certificateStoreLocation, out location))
                throw new Exception(string.Format("Could not find certificate store location '{0}'", certificateStoreLocation));
            return location;
        }

        protected static StoreName GetCertificateStoreName(string certificateStoreName)
        {
            StoreName name;
            if (!Enum.TryParse(certificateStoreName, out name))
                throw new Exception(string.Format("Could not find certificate store name '{0}'", certificateStoreName));
            return name;
        }
    }
}
