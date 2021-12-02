using System;
using System.Diagnostics;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.CheckpointBenchmark {
	internal static class Program {
		public static void Main(string[] args) {
			string path;
			int flushes;

			// CONFIGURATION
			if (args?.Length < 2) {
				path = @"C:\checkpoints\test.chk";
				flushes = 2000;
			} else {
				path = args[0];
				flushes = int.Parse(args[1]);
			}

			Console.WriteLine("Path: {0}", path);
			Console.WriteLine("Flushes: {0}", flushes);

			Measure(flushes, new MemoryMappedFileCheckpoint(path, "MemoryMappedFileCheckpoint", true));
			Measure(flushes, new FileCheckpoint(path,             "RegularFileCheckpoint     ", true));
		}

		public static void Measure(int numFlushes, ICheckpoint cp) {
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < numFlushes; i++) {
				cp.Write(i);
				cp.Flush();
				cp.Read();
			}
			cp.Dispose();

			var elapsed = sw.Elapsed;
			var flushesPerSecond = numFlushes / elapsed.TotalSeconds;
			Console.WriteLine("{0} did {1} flushes in {2}. {3:N0} flushes/second",
				cp.Name, numFlushes, elapsed, flushesPerSecond);
		}
	}
}
