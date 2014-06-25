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
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.Util;

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
            var options = new TOptions();
            try
            {
                Application.RegisterExitAction(Exit);

                options = EventStoreOptions.Parse<TOptions>(args, Opts.EnvPrefix);
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
                Log.FatalException(ex, "Unhandled exception while starting application:\n{0}", FormatExceptionMessage(ex));
                Application.Exit(ExitCode.Error, FormatExceptionMessage(ex));
            }
            finally
            {
                Log.Flush();
            }

            Application.ExitSilent(_exitCode, "Normal exit.");
            return _exitCode;
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
                    Application.Exit(4, "Appears that we are running in linux with a version 2 build of mono. This is generally not a good idea." +
                        "We recommend running with 3.0 or higher (3.2 especially). If you really want to run with this version of mono use --force to override this error.");
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
            LogManager.Init(componentName, logsDirectory);

            Log.Info("\n{0,-25} {1} ({2}/{3}, {4})\n"
                     + "{5,-25} {6} ({7})\n"
                     + "{8,-25} {9} ({10}-bit)\n"
                     + "{11,-25} {12}\n"
                     + "{13,-25} {14}\n\n"
                     + "{15}",
                     "ES VERSION:", VersionInfo.Version, VersionInfo.Branch, VersionInfo.Hashtag, VersionInfo.Timestamp,
                     "OS:", OS.OsFlavor, Environment.OSVersion,
                     "RUNTIME:", OS.GetRuntimeVersion(), Marshal.SizeOf(typeof(IntPtr)) * 8,
                     "GC:", GC.MaxGeneration == 0 ? "NON-GENERATION (PROBABLY BOEHM)" : string.Format("{0} GENERATIONS", GC.MaxGeneration + 1),
                     "LOGS:", LogManager.LogsDirectory,
                     EventStoreOptions.DumpOptions<TOptions>());

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

        protected static TFChunkDbConfig CreateDbConfig(string dbPath, int cachedChunks, long chunksCacheSize, bool inMemDb)
        {
            ICheckpoint writerChk;
            ICheckpoint chaserChk;
            ICheckpoint epochChk;
            ICheckpoint truncateChk;

            if (inMemDb)
            {
                writerChk = new InMemoryCheckpoint(Checkpoint.Writer);
                chaserChk = new InMemoryCheckpoint(Checkpoint.Chaser);
                epochChk = new InMemoryCheckpoint(Checkpoint.Epoch, initValue: -1);
                truncateChk = new InMemoryCheckpoint(Checkpoint.Truncate, initValue: -1);
            }
            else
            {
                if (!Directory.Exists(dbPath)) // mono crashes without this check
                    Directory.CreateDirectory(dbPath);

                var writerCheckFilename = Path.Combine(dbPath, Checkpoint.Writer + ".chk");
                var chaserCheckFilename = Path.Combine(dbPath, Checkpoint.Chaser + ".chk");
                var epochCheckFilename = Path.Combine(dbPath, Checkpoint.Epoch + ".chk");
                var truncateCheckFilename = Path.Combine(dbPath, Checkpoint.Truncate + ".chk");
                if (Runtime.IsMono)
                {
                    writerChk = new FileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
                    chaserChk = new FileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);
                    epochChk = new FileCheckpoint(epochCheckFilename, Checkpoint.Epoch, cached: true, initValue: -1);
                    truncateChk = new FileCheckpoint(truncateCheckFilename, Checkpoint.Truncate, cached: true, initValue: -1);
                }
                else
                {
                    writerChk = new MemoryMappedFileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
                    chaserChk = new MemoryMappedFileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);
                    epochChk = new MemoryMappedFileCheckpoint(epochCheckFilename, Checkpoint.Epoch, cached: true, initValue: -1);
                    truncateChk = new MemoryMappedFileCheckpoint(truncateCheckFilename, Checkpoint.Truncate, cached: true, initValue: -1);
                }
            }
            var cache = cachedChunks >= 0
                                ? cachedChunks*(long)(TFConsts.ChunkSize + ChunkHeader.Size + ChunkFooter.Size)
                                : chunksCacheSize;
            var nodeConfig = new TFChunkDbConfig(dbPath,
                                                 new VersionedPatternFileNamingStrategy(dbPath, "chunk-"),
                                                 TFConsts.ChunkSize,
                                                 cache,
                                                 writerChk,
                                                 chaserChk,
                                                 epochChk,
                                                 truncateChk,
                                                 inMemDb);
            return nodeConfig;
        }

        protected static X509Certificate2 LoadCertificateFromFile(string path, string password)
        {
            return new X509Certificate2(path, password);
        }

        protected static X509Certificate2 LoadCertificateFromStore(string certificateStoreLocation, string certificateStoreName, string certificateSubjectName, string certificateThumbprint)
        {
            X509Store store;

            if (!string.IsNullOrWhiteSpace(certificateStoreLocation))
            {
                StoreLocation location;
                if (!Enum.TryParse(certificateStoreLocation, out location))
                    throw new Exception(string.Format("Couldn't find certificate store location '{0}'", certificateStoreLocation));

                StoreName name;
                if (!Enum.TryParse(certificateStoreName, out name))
                    throw new Exception(string.Format("Couldn't find certificate store name '{0}'", certificateStoreName));

                store = new X509Store(name, location);
                
                try
                {
                    store.Open(OpenFlags.OpenExistingOnly);
                }
                catch (Exception exc)
                {
                    throw new Exception(string.Format("Couldn't open certificate store '{0}' in location {1}'.", name, location), exc);
                }
            }
            else
            {
                StoreName name;
                if (!Enum.TryParse(certificateStoreName, out name))
                    throw new Exception(string.Format("Couldn't find certificate store name '{0}'", certificateStoreName));

                store = new X509Store(name);

                try
                {
                    store.Open(OpenFlags.OpenExistingOnly);
                }
                catch (Exception exc)
                {
                    throw new Exception(string.Format("Couldn't open certificate store '{0}'.", name), exc);
                }
            }

            if (!string.IsNullOrWhiteSpace(certificateThumbprint))
            {
                var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, true);
                if (certificates.Count == 0)
                    throw new Exception(string.Format("Could not find valid certificate with thumbprint '{0}'.", certificateThumbprint));
                
                //Can this even happen?
                if (certificates.Count > 1)
                    throw new Exception(string.Format("Cannot determine a unique certificate from thumbprint '{0}'.", certificateThumbprint));

                return certificates[0];
            }
            
            if (!string.IsNullOrWhiteSpace(certificateSubjectName))
            {
                var certificates = store.Certificates.Find(X509FindType.FindBySubjectName, certificateSubjectName, true);
                if (certificates.Count == 0)
                    throw new Exception(string.Format("Could not find valid certificate with thumbprint '{0}'.", certificateThumbprint));

                //Can this even happen?
                if (certificates.Count > 1)
                    throw new Exception(string.Format("Cannot determine a unique certificate from thumbprint '{0}'.", certificateThumbprint));

                return certificates[0];
            }
            
            throw new ArgumentException("No thumbprint or subject name was specified for a certificate, but a certificate store was specified.");
        }
    }
}
