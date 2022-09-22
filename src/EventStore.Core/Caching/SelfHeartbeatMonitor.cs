using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Serilog;

namespace EventStore.Core.Caching {
	// Attempt to detect anything interrupting the systems ability to quickly pump tasks
	//qq move to another pr
	//qq maybe some of these things could be diagnosed automatically here
	// GC pauses - probably for compaction, correlated with a drop in the gen2 size in the stats
	// overloaded task queue - maybe we can check the queue length
	// VM pauses?

	// GC.GetGCMemoryInfo(GCKind.FullBlocking)
	public class SelfHeartbeatMonitor {
		public SelfHeartbeatMonitor(TimeSpan period, TimeSpan threshold) {
			_ = StartAsync(period, threshold);
		}

		private async Task StartAsync(TimeSpan period, TimeSpan threshold) {
			var sw = Stopwatch.StartNew();

			while (true) {
				try {
					var before = sw.Elapsed;
					await Task.Delay(period).ConfigureAwait(false);
					var after = sw.Elapsed;

					var actualPeriod = after - before;
					if (actualPeriod > threshold) {
						//qq just information prolly
						Log.Warning("Detected a delayed self heartbeat. Expected {expectedPeriod}. Actual {actualPeriod}",
							period, actualPeriod);
					}
				}
				catch (Exception ex) {
					Log.Error(ex, $"{nameof(SelfHeartbeatMonitor)} stopped");
				}
			}
		}
	}
}
