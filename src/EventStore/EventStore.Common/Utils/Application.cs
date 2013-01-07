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
        protected static readonly ILogger Log = LogManager.GetLoggerFor<Application>();

        private static Action<int> _exit;

        public static void RegisterExitAction(Action<int> exitAction)
        {
            Ensure.NotNull(exitAction, "exitAction");

            _exit = exitAction;
        }

        public static void Exit(ExitCode exitCode, string reason)
        {
            Exit((int) exitCode, reason);
        }

        public static void Exit(int exitCode, string reason)
        {
            Ensure.NotNullOrEmpty(reason, "reason");
            
            Console.WriteLine("Exiting with exitcode {0}\nExit reason : {1}", exitCode, reason);
            Log.Info("Exiting with exitcode {0}\nExit reason : {1}", exitCode, reason);

            LogManager.Finish();

            var exit = _exit;
            if (exit != null)
                exit(exitCode);
        }
    }
}