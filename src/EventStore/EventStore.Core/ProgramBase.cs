// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using EventStore.Common.Exceptions;
using EventStore.Common.Log;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core
{
    public abstract class ProgramBase<TOptions> where TOptions : IOptions, new()
    {
        protected readonly ILogger Log = LogManager.GetLoggerFor<ProgramBase<TOptions>>();

        private int _exitCode;
        private readonly ManualResetEventSlim _exitEvent = new ManualResetEventSlim(false);

        protected abstract string GetLogsDirectory(TOptions options);
        protected abstract string GetComponentName(TOptions options);

        protected abstract void Create(TOptions options);
        protected abstract void Start();
        public abstract void Stop();

        public int Run(string[] args)
        {
            if (args.Length == 1 && (args[0] == "--help" || args[0] == "/?"))
            {
                Console.WriteLine(new TOptions().GetUsage());
                return 0;
            }

            var options = new TOptions();
            try
            {
                Application.RegisterExitAction(Exit);

                options.Parse(args);
                if (options.ShowHelp)
                {
                    Console.WriteLine("Options:");
                    Console.WriteLine(options.GetUsage());
                    return 0;
                }

                Init(options);
                Create(options);
                Start();

                _exitEvent.Wait();
            }
            catch (OptionException exc)
            {
                Console.Error.WriteLine("Error while parsing options:");
                Console.Error.WriteLine(FormatExceptionMessage(exc));
                Console.Error.WriteLine();
                Console.Error.WriteLine("Usage:");
                Console.Error.WriteLine(options.GetUsage());
            }
            catch (ApplicationInitializationException ex)
            {
                Application.Exit(ExitCode.Error, FormatExceptionMessage(ex));
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Unhandled exception while starting application:\n{0}", FormatExceptionMessage(ex));
                Application.Exit(ExitCode.Error, FormatExceptionMessage(ex));
            }
            finally
            {
                Log.Flush();
            }

            return _exitCode;
        }

        private void Exit(int exitCode)
        {
            _exitCode = exitCode;
            _exitEvent.Set();
        }

        private void Init(TOptions options)
        {
            var projName = Assembly.GetEntryAssembly().GetName().Name.Replace(".", " - ");
            var componentName = GetComponentName(options);

            Console.Title = string.Format("{0}, {1}", projName, componentName);

            string logsDirectory = Path.GetFullPath(options.LogsDir.IsNotEmptyString() ? options.LogsDir : GetLogsDirectory(options));
            LogManager.Init(componentName, logsDirectory);

            Log.Info("\nOS: {0} ({1})\n"
                     + "RUNTIME: {2} ({3}-bit)\n"
                     + "GC: {4}\n"
                     + "LOGS: {5}\n\n"
                     + "{6}",
                     OS.IsLinux ? "Linux" : "Windows",
                     Environment.OSVersion,
                     OS.GetRuntimeVersion(),
                     Marshal.SizeOf(typeof(IntPtr)) * 8,
                     GC.MaxGeneration == 0 ? "NON-GENERATION (PROBABLY BOEHM)" : string.Format("{0} GENERATIONS", GC.MaxGeneration + 1),
                     LogManager.LogsDirectory,
                     options.DumpOptions());
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

        protected static TFChunkDbConfig CreateDbConfig(string dbPath, int chunksToCache)
        {
            if (!Directory.Exists(dbPath)) // mono crashes without this check
                Directory.CreateDirectory(dbPath);

            ICheckpoint writerChk;
            ICheckpoint chaserChk;

            var writerCheckFilename = Path.Combine(dbPath, Checkpoint.Writer + ".chk");
            var chaserCheckFilename = Path.Combine(dbPath, Checkpoint.Chaser + ".chk");
            if (Runtime.IsMono)
            {
                writerChk = new FileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
                chaserChk = new FileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);
            }
            else
            {
                writerChk = new MemoryMappedFileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
                chaserChk = new MemoryMappedFileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);
            }
            var nodeConfig = new TFChunkDbConfig(dbPath,
                                                 new VersionedPatternFileNamingStrategy(dbPath, "chunk-"),
                                                 TFConsts.ChunkSize,
                                                 chunksToCache,
                                                 writerChk,
                                                 chaserChk,
                                                 new[] {writerChk, chaserChk});
            return nodeConfig;
        }
    }
}
