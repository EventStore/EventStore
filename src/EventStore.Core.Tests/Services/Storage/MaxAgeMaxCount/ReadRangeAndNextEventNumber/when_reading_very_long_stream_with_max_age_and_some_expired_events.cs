using System;
using System.Diagnostics;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.MaxAgeMaxCount.ReadRangeAndNextEventNumber {
	// test the binary chop where it has to repeatedly lower the high bound
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class
		when_reading_very_long_stream_with_max_age_and_some_expired_events<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		public when_reading_very_long_stream_with_max_age_and_some_expired_events() : base(
			maxEntriesInMemTable: 500_000, chunkSize: TFConsts.ChunkSize) {
		}

		protected override void WriteTestScenario() {
			var now = DateTime.UtcNow;
			var metadata = string.Format(@"{{""$maxAge"":{0}}}", (int)TimeSpan.FromMinutes(20).TotalSeconds);
			WriteStreamMetadata("ES", 0, metadata, now.AddMinutes(-100));
			for (int i = 0; i < 20; i++) {
				WriteSingleEvent("ES", i, "bla", now.AddMinutes(-50), retryOnFail: true);
			}

			for (int i = 20; i < 1_000_000; i++) {
				WriteSingleEvent("ES", i, "bla", now.AddMinutes(-1), retryOnFail: true);
			}
		}

		[Test, Explicit, Category("LongRunning")]
		public void on_read_from_beginning() {
			Stopwatch sw = Stopwatch.StartNew();
			var res = ReadIndex.ReadStreamEventsForward("ES", 1, 10);
			var elapsed = sw.Elapsed;

			Assert.AreEqual(20, res.NextEventNumber);
			Assert.AreEqual(0, res.Records.Length);
			Assert.AreEqual(false, res.IsEndOfStream);

			res = ReadIndex.ReadStreamEventsForward("ES", res.NextEventNumber, 10);

			Assert.AreEqual(30, res.NextEventNumber);
			Assert.AreEqual(10, res.Records.Length);
			Assert.AreEqual(false, res.IsEndOfStream);

			Assert.Less(elapsed, TimeSpan.FromSeconds(1));
		}
	}
}
