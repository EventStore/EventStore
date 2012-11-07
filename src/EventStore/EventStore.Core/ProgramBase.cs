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
using System.Threading;
using EventStore.Common.CommandLine;
using EventStore.Common.CommandLine.lib;
using EventStore.Common.Configuration;
using EventStore.Common.Exceptions;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using System.Linq;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core
{
    public abstract class ProgramBase<TOptions> where TOptions : EventStoreCmdLineOptionsBase, new()
    {
        private static readonly ILogger Log = LogManager.GetLogger("ProgramBase");

        public bool BoxMode { get; set; }
        private int _exitCode;
        private readonly ManualResetEventSlim _exitEvent = new ManualResetEventSlim(false);

        public int Run(string[] args)
        {
            try
            {
                if (args.Length == 1 && (args[0] == "--help" || args[0] == "/?"))
                {
                    Console.WriteLine((new TOptions()).GetUsage());
                    return 0;
                }

                if (!BoxMode)
                {
                    Application.Start(exitAction: Exit);
                }

                var options = ParseAndInit(args);

                if (!BoxMode)
                {
                    var projName = Assembly.GetEntryAssembly().GetName().Name.Replace(".", " - ");
                    Console.Title = String.Format("{0}, {1}", projName, GetComponentName(options));
                }

                Create();
                Start();

                if (!BoxMode)
                {
                    _exitEvent.Wait();
                }
            }
            catch (ApplicationInitializationException ex)
            {
                Application.Exit(ExitCode.Error, FormatExceptionMessage(ex));
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Unhandled exception while starting application: {0}", FormatExceptionMessage(ex));
                Application.Exit(ExitCode.Error, FormatExceptionMessage(ex));
            }

            return _exitCode;
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

        protected abstract void OnArgsParsed(TOptions options);
        protected abstract string GetLogsDirectory();
        protected abstract string GetComponentName(TOptions options); 
        protected abstract void Create();
        protected abstract void Start();
        public abstract void Stop();

        private TOptions ParseAndInit(string[] args)
        {
            var options = new TOptions();
            if (!CommandLineParser.Default.ParseArguments(args, options, Console.Error, Constants.EnvVarPrefix))
                throw new ApplicationInitializationException("Error while parsing options");

            // todo MM: init should execute before OnArgsParsed, remove dependencies between log path and parsed options
            OnArgsParsed(options);
            Init(options);

            return options;
        }

        private void Init(TOptions options)
        {
            if (!BoxMode)
            {
                var logsDir = !string.IsNullOrEmpty(options.LogsDir) ? options.LogsDir : GetLogsDirectory();
                LogManager.Init(GetComponentName(options), logsDir);
            }

            var systemInfo = String.Format("{0} {1}", OS.IsLinux ? "Linux" : "Windows", Runtime.IsMono ? "MONO" : ".NET");
            var startInfo = String.Join(Environment.NewLine, options.GetLoadedOptionsPairs().Select(pair => String.Format("{0} : {1}", pair.Key, pair.Value)));
            var logsDirectory = String.Format("LOGS DIRECTORY : {0}", LogManager.LogsDirectory);
            
            var logger = LogManager.GetLoggerFor<ProgramBase<TOptions>>();
            logger.Info(String.Format("{0}{1}{2}{1}{3}", logsDirectory, Environment.NewLine, systemInfo, startInfo));
        }

        private void Exit(int exitCode)
        {
            _exitCode = exitCode;
            _exitEvent.Set();
        }

        protected static TFChunkDbConfig CreateDbConfig(string dbPath, int httpPort, DateTime timeStamp, int chunksToCache)
        {
            if (string.IsNullOrEmpty(dbPath))
                dbPath = GetAutoGeneratedPath(timeStamp, httpPort);

            var nodeDbConfig = CreateDbConfig(TFConsts.ChunkSize, dbPath, chunksToCache);
            return nodeDbConfig;
        }

        private static TFChunkDbConfig CreateDbConfig(int chunkSize, string dbPath, int chunksToCache)
        {
            if (!Directory.Exists(dbPath)) // mono crashes without this check
                Directory.CreateDirectory(dbPath);

            ICheckpoint writerChk;
            ICheckpoint chaserChk;

            if (Runtime.IsMono)
            {
                writerChk = new FileCheckpoint(Path.Combine(dbPath, Checkpoint.Writer + ".chk"), Checkpoint.Writer, cached: true);
                chaserChk = new FileCheckpoint(Path.Combine(dbPath, Checkpoint.Chaser + ".chk"), Checkpoint.Chaser, cached: true);
            }
            else
            {
                writerChk = new MemoryMappedFileCheckpoint(Path.Combine(dbPath, Checkpoint.Writer + ".chk"), Checkpoint.Writer, cached: true);
                chaserChk = new MemoryMappedFileCheckpoint(Path.Combine(dbPath, Checkpoint.Chaser + ".chk"), Checkpoint.Chaser, cached: true);
            }
            var nodeConfig = new TFChunkDbConfig(dbPath,
                                                 new VersionedPatternFileNamingStrategy(dbPath, "chunk-"),
                                                 chunkSize,
                                                 chunksToCache,
                                                 writerChk,
                                                 chaserChk,
                                                 new[] {writerChk, chaserChk});

            return nodeConfig;
        }

        protected static string GetAutoGeneratedPath(DateTime timeStamp, int port)
        {
            return Path.Combine(Path.GetTempPath(),
                                "EventStore",
                                string.Format("{0:yyyy-MM-dd_HH.mm.ss.ffffff}-Node{1}",
                                              timeStamp,
                                              port));
        }
    }
}
