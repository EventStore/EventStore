using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
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
                     "OS:", OS.OsFlavor, Environment.OSVersion,
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
