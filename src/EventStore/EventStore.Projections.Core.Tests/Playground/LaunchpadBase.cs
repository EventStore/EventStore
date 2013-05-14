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
using System.Diagnostics;
using System.IO;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using System.Threading.Tasks;

namespace EventStore.Projections.Core.Tests.Playground
{
    public class LaunchpadBase
    {
        protected readonly ILogger _logger = LogManager.GetLoggerFor<LaunchpadBase>();

        protected readonly Func<string, string, IDictionary<string, string>, IDisposable> _launch = StartHelperProcess;

        private class DisposeHandler : IDisposable
        {
            private readonly Action _dispose;

            public DisposeHandler(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                _dispose();
            }
        }

        private static IDisposable StartHelperProcess(
            string executable, string commandLine, IDictionary<string, string> additionalEnvironment)
        {
            foreach (var v in additionalEnvironment)
            {
                Environment.SetEnvironmentVariable(v.Key, v.Value);
            }

            var setup = new AppDomainSetup
                {ApplicationBase = AppDomain.CurrentDomain.BaseDirectory, ConfigurationFile = executable + ".config",};
            var appDomain = AppDomain.CreateDomain(
                Path.GetFileNameWithoutExtension(executable), AppDomain.CurrentDomain.Evidence, setup);
            Task.Factory.StartNew(
                () =>
                    {
                        try
                        {
                            var result = appDomain.ExecuteAssembly(executable, commandLine.Split(' '));
                            Console.WriteLine(executable + "has exited with code " + result);
                        }
                        catch (AppDomainUnloadedException)
                        {
                        }
                    });
            return new DisposeHandler(() => AppDomain.Unload(appDomain));
        }

        private static IDisposable RestartProcess(
            string executable, string commandLine, IDictionary<string, string> additionalEnvironment)
        {
            var existing = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable));
            foreach (var p in existing)
            {
                Console.WriteLine("Killing process {0} : {1}", p.Id, p.ProcessName);
                p.Kill();
            }
            var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    Arguments = commandLine,
                    FileName = executable,
                    //

                    //                    CreateNoWindow = true,
                    //                    RedirectStandardError = true,
                    //                    RedirectStandardOutput = true,
                };
            foreach (var pair in additionalEnvironment)
            {
                startInfo.EnvironmentVariables.Add(pair.Key, pair.Value);
            }
            var process = Process.Start(startInfo);
            return process;
        }

        protected class DumpingHandler : IHandle<Message>
        {
            public void Handle(Message message)
            {
                Console.WriteLine("Received: {0}({1})", message.GetType(), message);
            }
        }
    }
}
