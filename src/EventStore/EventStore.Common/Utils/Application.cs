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
using System.Collections.Generic;
using System.Threading;
using EventStore.Common.Log;

namespace EventStore.Common.Utils
{
    public enum ExitCode
    {
        Success = 0,
        Error = 1
    }

    public class Application
    {
        public const string AdditionalCommitChecks = "ADDITIONAL_COMMIT_CHECKS";
        public const string InfiniteMetastreams = "INFINITE_METASTREAMS";
        public const string DumpStatistics = "DUMP_STATISTICS";
        public const string DoNotTimeoutRequests = "DO_NOT_TIMEOUT_REQUESTS";
        public const string AlwaysKeepScavenged = "ALWAYS_KEEP_SCAVENGED";
        public const string DisableMergeChunks = "DISABLE_MERGE_CHUNKS";

        protected static readonly ILogger Log = LogManager.GetLoggerFor<Application>();

        private static Action<int> _exit;
        private static int _exited;

        private static readonly HashSet<string> _defines = new HashSet<string>();

        public static void RegisterExitAction(Action<int> exitAction)
        {
            Ensure.NotNull(exitAction, "exitAction");

            _exit = exitAction;
        }

        public static void ExitSilent(int exitCode, string reason)
        {
            Exit(exitCode, reason, silent: true);
        }

        public static void Exit(ExitCode exitCode, string reason)
        {
            Exit((int) exitCode, reason);
        }

        public static void Exit(int exitCode, string reason)
        {
            Exit(exitCode, reason, silent: false);
        }

        private static void Exit(int exitCode, string reason, bool silent)
        {
            if (Interlocked.CompareExchange(ref _exited, 1, 0) != 0)
                return;

            Ensure.NotNullOrEmpty(reason, "reason");

            if (!silent)
            {
                var message = string.Format("Exiting with exit code: {0}.\nExit reason: {1}", exitCode, reason);
                Console.WriteLine(message);
                if (exitCode != 0)
                    Log.Error(message);
                else
                    Log.Info(message);
            }

            var exit = _exit;
            if (exit != null)
                exit(exitCode);
        }

        public static void AddDefines(IEnumerable<string> defines)
        {
            foreach (var define in defines.Safe())
            {
                _defines.Add(define.ToUpper());
            }
        }

        public static bool IsDefined(string define)
        {
            Ensure.NotNull(define, "define");
            return _defines.Contains(define.ToUpper());
        }
    }
}