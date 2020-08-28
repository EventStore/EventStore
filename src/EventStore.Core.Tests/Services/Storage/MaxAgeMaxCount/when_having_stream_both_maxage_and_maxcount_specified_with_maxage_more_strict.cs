using System;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Data;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.MaxAgeMaxCount {
	[TestFixture]
	public class when_having_stream_both_maxage_and_maxcount_specified_with_maxage_more_strict : ReadIndexTestScenario {
		private EventRecord _r1;
		private EventRecord _r2;
		private EventRecord _r3;
		private EventRecord _r4;
		private EventRecord _r5;
		private EventRecord _r6;

		protected override void WriteTestScenario() {
			var now = DateTime.UtcNow;

			var metadata = string.Format(@"{{""$maxAge"":{0},""$maxCount"":4}}",
				(int)TimeSpan.FromMinutes(10).TotalSeconds);
			_r1 = WriteStreamMetadata("ES", 0, metadata);
			_r2 = WriteSingleEvent("ES", 0, "bla1", now.AddMinutes(-50));
			_r3 = WriteSingleEvent("ES", 1, "bla1", now.AddMinutes(-20));
			_r4 = WriteSingleEvent("ES", 2, "bla1", now.AddMinutes(-11));
			_r5 = WriteSingleEvent("ES", 3, "bla1", now.AddMinutes(-5));
			_r6 = WriteSingleEvent("ES", 4, "bla1", now.AddMinutes(-1));
		}

		[Test]
		public void single_event_read_doesnt_return_expired_events_and_returns_all_actual_ones() {
			var result = ReadIndex.ReadEvent("ES", 0);
			Assert.AreEqual(ReadEventResult.NotFound, result.Result);
			Assert.IsNull(result.Record);

			result = ReadIndex.ReadEvent("ES", 1);
			Assert.AreEqual(ReadEventResult.NotFound, result.Result);
			Assert.IsNull(result.Record);

			result = ReadIndex.ReadEvent("ES", 2);
			Assert.AreEqual(ReadEventResult.NotFound, result.Result);
			Assert.IsNull(result.Record);

			result = ReadIndex.ReadEvent("ES", 3);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_r5, result.Record);

			result = ReadIndex.ReadEvent("ES", 4);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_r6, result.Record);
		}

		[Test]
		public void forward_range_read_doesnt_return_expired_records() {
			var result = ReadIndex.ReadStreamEventsForward("ES", 0, 100);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(2, result.Records.Length);
			Assert.AreEqual(_r5, result.Records[0]);
			Assert.AreEqual(_r6, result.Records[1]);
		}

		[Test]
		public void backward_range_read_doesnt_return_expired_records() {
			var result = ReadIndex.ReadStreamEventsBackward("ES", -1, 100);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(2, result.Records.Length);
			Assert.AreEqual(_r6, result.Records[0]);
			Assert.AreEqual(_r5, result.Records[1]);
		}

		[Test]
		public void read_all_forward_returns_all_records_including_expired_ones() {
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
		public void read_all_backward_returns_all_records_including_expired_ones() {
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
