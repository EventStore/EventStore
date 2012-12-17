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
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using EventStore.Common.Utils;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Playground
{
    [TestFixture]
    public class Launchpad2 : LaunchpadBase
    {
        private IDisposable _vnodeProcess;
        private IDisposable _clientProcess;

        private string _binFolder;
        private Dictionary<string, string> _environment;
        private string _dbPath;

        [SetUp]
        public void Setup()
        {
            if (!OS.IsLinux)
                AllocConsole(); // this is required to keep console open after executeassemly has exited

            _binFolder = AppDomain.CurrentDomain.BaseDirectory;
            _dbPath = Path.Combine(_binFolder, DateTime.UtcNow.Ticks.ToString());
            _environment = new Dictionary<string, string> {{"EVENTSTORE_LOGSDIR", _dbPath}};

            var vnodeExecutable = Path.Combine(_binFolder, @"EventStore.Projections.Worker.exe");


            string vnodeCommandLine =
                string.Format(
                    @"--ip=127.0.0.1 --db={0} --stats-frequency-sec=10 --int-tcp-port=3111 --ext-tcp-port=1111 --http-port=2111",
                    _dbPath);

            _vnodeProcess = _launch(vnodeExecutable, vnodeCommandLine, _environment);
        }

        [TearDown]
        public void Teardown()
        {
            if (_vnodeProcess != null) _vnodeProcess.Dispose();
            if (_clientProcess != null) _clientProcess.Dispose();
        }

        public void LaunchFlood()
        {
            var clientExecutable = Path.Combine(_binFolder, @"EventStore.Client.exe");
            string clientCommandLine = @"--ip 127.0.0.1 --tcp-port 1111 --http-port 2111 WRFL 1 100";
            _clientProcess = _launch(clientExecutable, clientCommandLine, _environment);
        }

        [Test, Explicit, Category("LongRunning")]
        public void RunSingle()
        {
            Thread.Sleep(60000);
        }

        [Test, Explicit, Category("LongRunning")]
        public void RunSingleAndFlood()
        {
            Thread.Sleep(4000);
            LaunchFlood();
            Thread.Sleep(10000);
            LaunchFlood();
            Thread.Sleep(20000);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();
    }
}
