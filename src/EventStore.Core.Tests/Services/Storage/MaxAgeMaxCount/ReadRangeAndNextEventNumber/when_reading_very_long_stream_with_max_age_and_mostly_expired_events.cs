using System;
using System.Diagnostics;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.MaxAgeMaxCount.ReadRangeAndNextEventNumber {
	public class
		when_reading_very_long_stream_with_max_age_and_mostly_expired_events : ReadIndexTestScenario {
		public when_reading_very_long_stream_with_max_age_and_mostly_expired_events() : base(
			maxEntriesInMemTable: 500_000, chunkSize: TFConsts.ChunkSize) {
		}

		protected override void WriteTestScenario() {
			var now = DateTime.UtcNow;
			var metadata = string.Format(@"{{""$maxAge"":{0}}}", (int)TimeSpan.FromMinutes(20).TotalSeconds);
			WriteStreamMetadata("ES", 0, metadata, now.AddMinutes(-100));
			for (int i = 0; i < 1_000_000; i++) {
				WriteSingleEvent("ES", i, "bla", now.AddMinutes(-50), retryOnFail: true);
			}

			for (int i = 1_000_000; i < 1_000_010; i++) {
				WriteSingleEvent("ES", i, "bla", now.AddMinutes(-1), retryOnFail: true);
			}
		}

		[Test, Explicit, Category("LongRunning")]
		public void on_read_from_beginning() {
			Stopwatch sw = Stopwatch.StartNew();
			var res = ReadIndex.ReadStreamEventsForward("ES", 1, 10);
			var elapsed = sw.Elapsed;
			Assert.Less(elapsed, TimeSpan.FromSeconds(1));
			Assert.AreEqual(9, res.Records.Length);
		}
	}
}
