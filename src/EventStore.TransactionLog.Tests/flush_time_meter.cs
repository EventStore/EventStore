using System;
using System.Diagnostics;
using EventStore.Core.TransactionLog.Tests.Helpers;
using EventStore.Native.FileAccess;
using NUnit.Framework;

namespace EventStore.Core.TransactionLog.Tests {
	[TestFixture]
	public class flush_time_meter : SpecificationWithFile {
		[Test, Ignore("long running")]
		public void flush_time_test() {
			var rnd = new Random();
			var sw = Stopwatch.StartNew();
			var gw = Stopwatch.StartNew();
			//using (var fs = new FileStream(Filename, FileMode.OpenOrCreate)) {
			using (var fs = new NativeFileStream(Filename)) {
				const int iter = 1000;
				for (int bytes = 100; bytes < 1000000; bytes *= 2) {
					var arr = new byte[bytes];
					rnd.NextBytes(arr);

					TimeSpan min = TimeSpan.FromHours(1);
					TimeSpan max = TimeSpan.Zero;

					gw.Restart();
					for (int i = 0; i < iter; ++i) {
						fs.Write(arr, 0, arr.Length);

						sw.Restart();
						fs.Flush();
						var elapsed = sw.Elapsed;

						min = elapsed < min ? elapsed : min;
						max = elapsed > max ? elapsed : max;
					}

					gw.Stop();

					Console.WriteLine("{0} bytes, Min: {1}, Max: {2}, Avg: {3}",
						bytes,
						min,
						max,
						TimeSpan.FromTicks(gw.Elapsed.Ticks / iter));
				}
			}
		}
	}
}
