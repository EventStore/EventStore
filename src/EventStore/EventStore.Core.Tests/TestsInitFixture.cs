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
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests
{
    [SetUpFixture]
    public class TestsInitFixture
    {
        [SetUp]
        public void SetUp()
        {
            Console.WriteLine("Initializing tests (setting console loggers)...");
            LogManager.SetLogFactory(x => new ConsoleLogger(x));
            Application.AddDefines(new[] { Application.AdditionalCommitChecks });
            LogEnvironmentInfo();

            if (!Debugger.IsAttached)
                PortsHelper.InitPorts(IPAddress.Loopback);
        }

        private void LogEnvironmentInfo()
        {
            var log = LogManager.GetLoggerFor<TestsInitFixture>();

            log.Info("\n{0,-25} {1} ({2}/{3}, {4})\n"
                     + "{5,-25} {6} ({7})\n"
                     + "{8,-25} {9} ({10}-bit)\n"
                     + "{11,-25} {12}\n\n",
                     "ES VERSION:", VersionInfo.Version, VersionInfo.Branch, VersionInfo.Hashtag, VersionInfo.Timestamp,
                     "OS:", OS.IsLinux ? "Linux" : "Windows", Environment.OSVersion,
                     "RUNTIME:", OS.GetRuntimeVersion(), Marshal.SizeOf(typeof(IntPtr)) * 8,
                     "GC:", GC.MaxGeneration == 0 ? "NON-GENERATION (PROBABLY BOEHM)" : string.Format("{0} GENERATIONS", GC.MaxGeneration + 1));
        }

        [TearDown]
        public void TearDown()
        {
            var runCount = Math.Max(1, MiniNode.RunCount);
            var msg = string.Format("Total running time of MiniNode: {0} (mean {1})\n" +
                                    "Total starting time of MiniNode: {2} (mean {3})\n" +
                                    "Total stopping time of MiniNode: {4} (mean {5})\n" +
                                    "Total run count: {6}",
                                    MiniNode.RunningTime.Elapsed, TimeSpan.FromTicks(MiniNode.RunningTime.Elapsed.Ticks/runCount),
                                    MiniNode.StartingTime.Elapsed, TimeSpan.FromTicks(MiniNode.StartingTime.Elapsed.Ticks/runCount),
                                    MiniNode.StoppingTime.Elapsed, TimeSpan.FromTicks(MiniNode.StoppingTime.Elapsed.Ticks/runCount),
                                    MiniNode.RunCount);
            Console.WriteLine(msg);
            LogManager.Finish();
        }
    }
}
