using System;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.BuildingIndex {
	[TestFixture]
	public class when_building_an_index_off_tfile_with_prepares_but_no_commits : ReadIndexTestScenario {
		protected override void WriteTestScenario() {
			long p1;
			Writer.Write(new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "test1", -1, DateTime.UtcNow,
					PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]),
				out p1);
			long p2;
			Writer.Write(new PrepareLogRecord(p1, Guid.NewGuid(), Guid.NewGuid(), p1, 0, "test2", -1, DateTime.UtcNow,
					PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]),
				out p2);
			long p3;
			Writer.Write(new PrepareLogRecord(p2, Guid.NewGuid(), Guid.NewGuid(), p2, 0, "test3", -1, DateTime.UtcNow,
					PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]),
				out p3);
		}

		[Test]
		public void the_first_stream_is_not_in_index_yet() {
			var result = ReadIndex.ReadEvent("test1", 0);
			Assert.AreEqual(ReadEventResult.NoStream, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void the_second_stream_is_not_in_index_yet() {
			var result = ReadIndex.ReadEvent("test2", 0);
			Assert.AreEqual(ReadEventResult.NoStream, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void the_last_event_is_not_returned_for_stream() {
			var result = ReadIndex.ReadEvent("test2", -1);
			Assert.AreEqual(ReadEventResult.NoStream, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void read_all_events_forward_returns_no_events() {
			var result = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 10);
			Assert.AreEqual(0, result.Records.Count);
		}

		[Test]
		public void read_all_events_backward_returns_no_events() {
			var result = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 10);
			Assert.AreEqual(0, result.Records.Count);
		}
	}
}
