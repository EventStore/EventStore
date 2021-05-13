using System;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.MaxAgeMaxCount {
	[TestFixture, Ignore("Metadata must be valid.")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class with_invalid_max_count_and_normal_max_age<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		private EventRecord _r1;
		private EventRecord _r2;
		private EventRecord _r3;
		private EventRecord _r4;
		private EventRecord _r5;
		private EventRecord _r6;

		protected override void WriteTestScenario() {
			var now = DateTime.UtcNow;

			const string metadata = @"{""$maxAge"":21,""$maxCount"":2.1}";

			_r1 = WriteStreamMetadata("ES", 0, metadata);
			_r2 = WriteSingleEvent("ES", 0, "bla1", now.AddSeconds(-50));
			_r3 = WriteSingleEvent("ES", 1, "bla1", now.AddSeconds(-20));
			_r4 = WriteSingleEvent("ES", 2, "bla1", now.AddSeconds(-11));
			_r5 = WriteSingleEvent("ES", 3, "bla1", now.AddSeconds(-5));
			_r6 = WriteSingleEvent("ES", 4, "bla1", now.AddSeconds(-1));
		}

		[Test]
		public void on_single_event_read_invalid_value_is_ignored() {
			var result = ReadIndex.ReadEvent("ES", 0);
			Assert.AreEqual(ReadEventResult.NotFound, result.Result);
			Assert.IsNull(result.Record);

			result = ReadIndex.ReadEvent("ES", 1);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_r3, result.Record);

			result = ReadIndex.ReadEvent("ES", 2);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_r4, result.Record);

			result = ReadIndex.ReadEvent("ES", 3);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_r5, result.Record);

			result = ReadIndex.ReadEvent("ES", 4);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_r6, result.Record);
		}

		[Test]
		public void on_forward_range_read_invalid_value_is_ignored() {
			var result = ReadIndex.ReadStreamEventsForward("ES", 0, 100);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(4, result.Records.Length);
			Assert.AreEqual(_r3, result.Records[0]);
			Assert.AreEqual(_r4, result.Records[1]);
			Assert.AreEqual(_r5, result.Records[2]);
			Assert.AreEqual(_r6, result.Records[3]);
		}

		[Test]
		public void on_backward_range_read_invalid_value_is_ignored() {
			var result = ReadIndex.ReadStreamEventsBackward("ES", -1, 100);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(4, result.Records.Length);
			Assert.AreEqual(_r6, result.Records[0]);
			Assert.AreEqual(_r5, result.Records[1]);
			Assert.AreEqual(_r4, result.Records[2]);
			Assert.AreEqual(_r3, result.Records[3]);
		}

		[Test]
		public void on_read_all_forward_both_values_are_ignored() {
			var records = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records;
			Assert.AreEqual(6, records.Count);
			Assert.AreEqual(_r1, records[0].Event);
			Assert.AreEqual(_r2, records[1].Event);
			Assert.AreEqual(_r3, records[2].Event);
			Assert.AreEqual(_r4, records[3].Event);
			Assert.AreEqual(_r5, records[4].Event);
			Assert.AreEqual(_r6, records[5].Event);
		}

		[Test]
		public void on_read_all_backward_both_values_are_ignored() {
			var records = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100).Records;
			Assert.AreEqual(6, records.Count);
			Assert.AreEqual(_r6, records[0].Event);
			Assert.AreEqual(_r5, records[1].Event);
			Assert.AreEqual(_r4, records[2].Event);
			Assert.AreEqual(_r3, records[3].Event);
			Assert.AreEqual(_r2, records[4].Event);
			Assert.AreEqual(_r1, records[5].Event);
		}
	}
}
