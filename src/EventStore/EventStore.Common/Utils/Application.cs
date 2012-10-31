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
using EventStore.Common.CommandLine;
using EventStore.Common.Log;

namespace EventStore.Common.Utils
{
    public class Application
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<Application>();
        private static Action<int> _exit;
        private static bool _initialized;

        public static void Start(Action<int> exitAction)
        {
            if (_initialized)
                throw new InvalidOperationException("Application is already initialized");

            _exit = exitAction;
            _initialized = true;
        }

        public static void Exit(ExitCode exitCode, string reason)
        {
            if (!_initialized)
                throw new InvalidOperationException("Application should be initialized before exiting");
            Ensure.NotNullOrEmpty(reason, "reason");

            Console.WriteLine("Exiting...");
            Console.WriteLine("Exit reason : {0}", reason);

            LogManager.Finish();
            _exit((int)exitCode);
        }
    }

    public enum ExitCode
    {
        Success = 0,
        Error
    }
}