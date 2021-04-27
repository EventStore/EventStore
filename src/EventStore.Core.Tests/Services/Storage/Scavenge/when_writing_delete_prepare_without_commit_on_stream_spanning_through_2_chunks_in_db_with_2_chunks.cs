using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.Scavenge {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_writing_delete_prepare_without_commit_and_scavenging<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		private EventRecord _event0;
		private EventRecord _event1;

		protected override void WriteTestScenario() {
			_event0 = WriteSingleEvent("ES", 0, "bla1");
			_streamNameIndex.GetOrAddId("ES", out var esStreamId);
			var prepare = LogRecord.DeleteTombstone(_recordFactory, WriterCheckpoint.ReadNonFlushed(), Guid.NewGuid(), Guid.NewGuid(),
				esStreamId, 2);
			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));

			_event1 = WriteSingleEvent("ES", 1, "bla1");
			Scavenge(completeLast: false, mergeChunks: false);
		}

		[Test]
		public void read_one_by_one_returns_all_commited_events() {
			var result = ReadIndex.ReadEvent("ES", 0);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_event0, result.Record);

			result = ReadIndex.ReadEvent("ES", 1);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_event1, result.Record);
		}

		[Test]
		public void read_stream_events_forward_should_return_all_events() {
			var result = ReadIndex.ReadStreamEventsForward("ES", 0, 100);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(2, result.Records.Length);
			Assert.AreEqual(_event0, result.Records[0]);
			Assert.AreEqual(_event1, result.Records[1]);
		}

		[Test]
		public void read_stream_events_backward_should_return_stream_deleted() {
			var result = ReadIndex.ReadStreamEventsBackward("ES", -1, 100);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(2, result.Records.Length);
			Assert.AreEqual(_event1, result.Records[0]);
			Assert.AreEqual(_event0, result.Records[1]);
		}

		[Test]
		public void read_all_forward_returns_all_events() {
			var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records.Select(r => r.Event).ToArray();
			Assert.AreEqual(2, events.Length);
			Assert.AreEqual(_event0, events[0]);
			Assert.AreEqual(_event1, events[1]);
		}

		[Test]
		public void read_all_backward_returns_all_events() {
			var events = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100).Records.Select(r => r.Event)
				.ToArray();
			Assert.AreEqual(2, events.Length);
			Assert.AreEqual(_event1, events[0]);
			Assert.AreEqual(_event0, events[1]);
		}
	}
}
