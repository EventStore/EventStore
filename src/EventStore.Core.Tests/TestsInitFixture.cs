using System;
using System.Net;
using System.Runtime.InteropServices;
using EventStore.Common.Utils;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using Serilog;

namespace EventStore.Core.Tests {
	[SetUpFixture]
	public class TestsInitFixture {
		[OneTimeSetUp]
		public void SetUp() {
			SetUp(Serilog.Events.LogEventLevel.Warning);
		}

		public void SetUp(Serilog.Events.LogEventLevel minimumLevel) {
			ServicePointManager.DefaultConnectionLimit = 1000;

			// https://github.com/nunit/nunit/issues/1062#issuecomment-259589577
			// Standard output is produced using Console.Write and TextContext.Write. The text is attached to the test result.
			// Immediate output is produced by Console.Error, TextContext.Error and TextContext.Progress. It is displayed immediately when received.
			// Normally, the console only displays info from the fixture if the OneTimeSetUp fails.
			//
			// Therefore, here we set up the logger to log whatever we want to see to the console error output
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Is(minimumLevel)
				.MinimumLevel.Override("EventStore.Core.Tests", Serilog.Events.LogEventLevel.Debug)
				.WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
				.CreateLogger();
			LogEnvironmentInfo();
		}

		private void LogEnvironmentInfo() {
			var log = Log.ForContext<TestsInitFixture>();

			log.Information("\n{0,-25} {1} ({2}/{3}, {4})\n"
					 + "{5,-25} {6} ({7})\n"
					 + "{8,-25} {9} ({10}-bit)\n"
					 + "{11,-25} {12}\n\n",
				"ES VERSION:", VersionInfo.Version, VersionInfo.Branch, VersionInfo.Hashtag, VersionInfo.Timestamp,
				"OS:", OS.OsFlavor, Environment.OSVersion,
				"RUNTIME:", OS.GetRuntimeVersion(), Marshal.SizeOf(typeof(IntPtr)) * 8,
				"GC:",
				GC.MaxGeneration == 0
					? "NON-GENERATION (PROBABLY BOEHM)"
					: string.Format("{0} GENERATIONS", GC.MaxGeneration + 1));
		}

		[OneTimeTearDown]
		public void TearDown() {
			var log = Log.ForContext<TestsInitFixture>();
			var runCount = Math.Max(1, MiniNode<LogFormat.V2,string>.RunCount);
			var msg = string.Format(
				"\n" +
				"Total running  time of MiniNode (Log V2): {0} (mean {1})\n" +
				"Total starting time of MiniNode (Log V2): {2} (mean {3})\n" +
				"Total stopping time of MiniNode (Log V2): {4} (mean {5})\n" +
				"Total run count (Log V2): {6}",
				MiniNode<LogFormat.V2, string>.RunningTime.Elapsed, TimeSpan.FromTicks(MiniNode<LogFormat.V2, string>.RunningTime.Elapsed.Ticks / runCount),
				MiniNode<LogFormat.V2, string>.StartingTime.Elapsed, TimeSpan.FromTicks(MiniNode<LogFormat.V2, string>.StartingTime.Elapsed.Ticks / runCount),
				MiniNode<LogFormat.V2, string>.StoppingTime.Elapsed, TimeSpan.FromTicks(MiniNode<LogFormat.V2, string>.StoppingTime.Elapsed.Ticks / runCount),
				MiniNode<LogFormat.V2, string>.RunCount);

			log.Information(msg);

			runCount = Math.Max(1, MiniNode<LogFormat.V3, uint>.RunCount);
			msg = string.Format(
				"\n" +
				"Total running  time of MiniNode (Log V3): {0} (mean {1})\n" +
				"Total starting time of MiniNode (Log V3): {2} (mean {3})\n" +
				"Total stopping time of MiniNode (Log V3): {4} (mean {5})\n" +
				"Total run count (Log V3): {6}",
				MiniNode<LogFormat.V3, uint>.RunningTime.Elapsed, TimeSpan.FromTicks(MiniNode<LogFormat.V3, uint>.RunningTime.Elapsed.Ticks / runCount),
				MiniNode<LogFormat.V3, uint>.StartingTime.Elapsed, TimeSpan.FromTicks(MiniNode<LogFormat.V3, uint>.StartingTime.Elapsed.Ticks / runCount),
				MiniNode<LogFormat.V3, uint>.StoppingTime.Elapsed, TimeSpan.FromTicks(MiniNode<LogFormat.V3, uint>.StoppingTime.Elapsed.Ticks / runCount),
				MiniNode<LogFormat.V3, uint>.RunCount);

			log.Information(msg);
		}
	}
}
