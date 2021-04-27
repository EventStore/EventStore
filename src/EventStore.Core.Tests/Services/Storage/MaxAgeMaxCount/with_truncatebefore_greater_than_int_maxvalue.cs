using System;
using EventStore.Core.Data;
using EventStore.Core.Services;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.MaxAgeMaxCount {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class with_truncatebefore_greater_than_int_maxvalue<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		private EventRecord _r1;
		private EventRecord _r2;
		private EventRecord _r3;
		private EventRecord _r4;
		private EventRecord _r5;
		private EventRecord _r6;

		private const long first = (long)int.MaxValue + 1;
		private const long second = (long)int.MaxValue + 2;
		private const long third = (long)int.MaxValue + 3;
		private const long fourth = (long)int.MaxValue + 4;
		private const long fifth = (long)int.MaxValue + 5;

		protected override void WriteTestScenario() {
			var now = DateTime.UtcNow;

			string metadata = @"{""$tb"":" + third + "}";

			_r1 = WriteStreamMetadata("ES", 0, metadata, now.AddSeconds(-100));
			_r2 = WriteSingleEvent("ES", first, "bla1", now.AddSeconds(-50));
			_r3 = WriteSingleEvent("ES", second, "bla1", now.AddSeconds(-20));
			_r4 = WriteSingleEvent("ES", third, "bla1", now.AddSeconds(-11));
			_r5 = WriteSingleEvent("ES", fourth, "bla1", now.AddSeconds(-5));
			_r6 = WriteSingleEvent("ES", fifth, "bla1", now.AddSeconds(-1));
		}

		[Test]
		public void metastream_read_returns_metaevent() {
			var result = ReadIndex.ReadEvent(SystemStreams.MetastreamOf("ES"), 0);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_r1, result.Record);
		}

		[Test]
		public void single_event_read_returns_records_after_truncate_before() {
			var result = ReadIndex.ReadEvent("ES", first);
			Assert.AreEqual(ReadEventResult.NotFound, result.Result);
			Assert.IsNull(result.Record);

			result = ReadIndex.ReadEvent("ES", second);
			Assert.AreEqual(ReadEventResult.NotFound, result.Result);
			Assert.IsNull(result.Record);

			result = ReadIndex.ReadEvent("ES", third);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_r4, result.Record);

			result = ReadIndex.ReadEvent("ES", fourth);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_r5, result.Record);

			result = ReadIndex.ReadEvent("ES", fifth);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_r6, result.Record);
		}

		[Test]
		public void forward_range_read_returns_records_after_truncate_before() {
			var result = ReadIndex.ReadStreamEventsForward("ES", first, 100);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(3, result.Records.Length);
			Assert.AreEqual(_r4, result.Records[0]);
			Assert.AreEqual(_r5, result.Records[1]);
			Assert.AreEqual(_r6, result.Records[2]);
		}

		[Test]
		public void backward_range_read_returns_records_after_truncate_before() {
			var result = ReadIndex.ReadStreamEventsBackward("ES", -1, 100);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(3, result.Records.Length);
			Assert.AreEqual(_r6, result.Records[0]);
			Assert.AreEqual(_r5, result.Records[1]);
			Assert.AreEqual(_r4, result.Records[2]);
		}

		[Test]
		public void read_all_forward_returns_all_records() {
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
		public void read_all_backward_returns_all_records() {
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
