using System.Linq;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class when_having_truncatebefore_set_for_stream : SpecificationWithMiniNode {
		private EventData[] _testEvents;

		protected override void When() {
			_testEvents = Enumerable.Range(0, 5).Select(x => TestEvent.NewTestEvent(data: x.ToString())).ToArray();
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void read_event_respects_truncatebefore() {
			const string stream = "read_event_respects_truncatebefore";
			_conn.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, _testEvents).Wait();

			_conn.SetStreamMetadataAsync(stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetTruncateBefore(2)).Wait();

			var res = _conn.ReadEventAsync(stream, 1, false).Result;
			Assert.AreEqual(EventReadStatus.NotFound, res.Status);

			res = _conn.ReadEventAsync(stream, 2, false).Result;
			Assert.AreEqual(EventReadStatus.Success, res.Status);
			Assert.AreEqual(_testEvents[2].EventId, res.Event.Value.OriginalEvent.EventId);
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void read_stream_forward_respects_truncatebefore() {
			const string stream = "read_stream_forward_respects_truncatebefore";
			_conn.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, _testEvents).Wait();

			_conn.SetStreamMetadataAsync(stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetTruncateBefore(2)).Wait();

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Select(x => x.Event.EventId).ToArray());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void read_stream_backward_respects_truncatebefore() {
			const string stream = "read_stream_backward_respects_truncatebefore";
			_conn.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, _testEvents).Wait();

			_conn.SetStreamMetadataAsync(stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetTruncateBefore(2)).Wait();

			var res = _conn.ReadStreamEventsBackwardAsync(stream, -1, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void after_setting_less_strict_truncatebefore_read_event_reads_more_events() {
			const string stream = "after_setting_less_strict_truncatebefore_read_event_reads_more_events";

			_conn.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, _testEvents).Wait();

			_conn.SetStreamMetadataAsync(stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetTruncateBefore(2)).Wait();

			var res = _conn.ReadEventAsync(stream, 1, false).Result;
			Assert.AreEqual(EventReadStatus.NotFound, res.Status);

			res = _conn.ReadEventAsync(stream, 2, false).Result;
			Assert.AreEqual(EventReadStatus.Success, res.Status);
			Assert.AreEqual(_testEvents[2].EventId, res.Event.Value.OriginalEvent.EventId);

			_conn.SetStreamMetadataAsync(stream, 0, StreamMetadata.Build().SetTruncateBefore(1)).Wait();

			res = _conn.ReadEventAsync(stream, 0, false).Result;
			Assert.AreEqual(EventReadStatus.NotFound, res.Status);

			res = _conn.ReadEventAsync(stream, 1, false).Result;
			Assert.AreEqual(EventReadStatus.Success, res.Status);
			Assert.AreEqual(_testEvents[1].EventId, res.Event.Value.OriginalEvent.EventId);
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void after_setting_more_strict_truncatebefore_read_event_reads_less_events() {
			const string stream = "after_setting_more_strict_truncatebefore_read_event_reads_less_events";

			_conn.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, _testEvents).Wait();

			_conn.SetStreamMetadataAsync(stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetTruncateBefore(2)).Wait();

			var res = _conn.ReadEventAsync(stream, 1, false).Result;
			Assert.AreEqual(EventReadStatus.NotFound, res.Status);

			res = _conn.ReadEventAsync(stream, 2, false).Result;
			Assert.AreEqual(EventReadStatus.Success, res.Status);
			Assert.AreEqual(_testEvents[2].EventId, res.Event.Value.OriginalEvent.EventId);

			_conn.SetStreamMetadataAsync(stream, 0, StreamMetadata.Build().SetTruncateBefore(3)).Wait();

			res = _conn.ReadEventAsync(stream, 2, false).Result;
			Assert.AreEqual(EventReadStatus.NotFound, res.Status);

			res = _conn.ReadEventAsync(stream, 3, false).Result;
			Assert.AreEqual(EventReadStatus.Success, res.Status);
			Assert.AreEqual(_testEvents[3].EventId, res.Event.Value.OriginalEvent.EventId);
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void less_strict_max_count_doesnt_change_anything_for_event_read() {
			const string stream = "less_strict_max_count_doesnt_change_anything_for_event_read";

			_conn.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, _testEvents).Wait();

			_conn.SetStreamMetadataAsync(stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetTruncateBefore(2)).Wait();

			var res = _conn.ReadEventAsync(stream, 1, false).Result;
			Assert.AreEqual(EventReadStatus.NotFound, res.Status);

			res = _conn.ReadEventAsync(stream, 2, false).Result;
			Assert.AreEqual(EventReadStatus.Success, res.Status);
			Assert.AreEqual(_testEvents[2].EventId, res.Event.Value.OriginalEvent.EventId);

			_conn.SetStreamMetadataAsync(stream, 0, StreamMetadata.Build().SetTruncateBefore(2).SetMaxCount(4)).Wait();

			res = _conn.ReadEventAsync(stream, 1, false).Result;
			Assert.AreEqual(EventReadStatus.NotFound, res.Status);

			res = _conn.ReadEventAsync(stream, 2, false).Result;
			Assert.AreEqual(EventReadStatus.Success, res.Status);
			Assert.AreEqual(_testEvents[2].EventId, res.Event.Value.OriginalEvent.EventId);
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void more_strict_max_count_gives_less_events_for_event_read() {
			const string stream = "more_strict_max_count_gives_less_events_for_event_read";

			_conn.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, _testEvents).Wait();

			_conn.SetStreamMetadataAsync(stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetTruncateBefore(2)).Wait();

			var res = _conn.ReadEventAsync(stream, 1, false).Result;
			Assert.AreEqual(EventReadStatus.NotFound, res.Status);

			res = _conn.ReadEventAsync(stream, 2, false).Result;
			Assert.AreEqual(EventReadStatus.Success, res.Status);
			Assert.AreEqual(_testEvents[2].EventId, res.Event.Value.OriginalEvent.EventId);

			_conn.SetStreamMetadataAsync(stream, 0, StreamMetadata.Build().SetTruncateBefore(2).SetMaxCount(2)).Wait();

			res = _conn.ReadEventAsync(stream, 2, false).Result;
			Assert.AreEqual(EventReadStatus.NotFound, res.Status);

			res = _conn.ReadEventAsync(stream, 3, false).Result;
			Assert.AreEqual(EventReadStatus.Success, res.Status);
			Assert.AreEqual(_testEvents[3].EventId, res.Event.Value.OriginalEvent.EventId);
		}


		[Test, Category("LongRunning"), Category("Network")]
		public void after_setting_less_strict_truncatebefore_read_stream_forward_reads_more_events() {
			const string stream = "after_setting_less_strict_truncatebefore_read_stream_forward_reads_more_events";

			_conn.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, _testEvents).Wait();

			_conn.SetStreamMetadataAsync(stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetTruncateBefore(2)).Wait();

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Select(x => x.Event.EventId).ToArray());

			_conn.SetStreamMetadataAsync(stream, 0, StreamMetadata.Build().SetTruncateBefore(1)).Wait();

			res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(4, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(1).Select(x => x.EventId).ToArray(),
				res.Events.Select(x => x.Event.EventId).ToArray());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void after_setting_more_strict_truncatebefore_read_stream_forward_reads_less_events() {
			const string stream = "after_setting_more_strict_truncatebefore_read_stream_forward_reads_less_events";

			_conn.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, _testEvents).Wait();

			_conn.SetStreamMetadataAsync(stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetTruncateBefore(2)).Wait();

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Select(x => x.Event.EventId).ToArray());

			_conn.SetStreamMetadataAsync(stream, 0, StreamMetadata.Build().SetTruncateBefore(3)).Wait();

			res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(2, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(3).Select(x => x.EventId).ToArray(),
				res.Events.Select(x => x.Event.EventId).ToArray());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void less_strict_max_count_doesnt_change_anything_for_stream_forward_read() {
			const string stream = "less_strict_max_count_doesnt_change_anything_for_stream_forward_read";

			_conn.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, _testEvents).Wait();

			_conn.SetStreamMetadataAsync(stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetTruncateBefore(2)).Wait();

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Select(x => x.Event.EventId).ToArray());

			_conn.SetStreamMetadataAsync(stream, 0, StreamMetadata.Build().SetTruncateBefore(2).SetMaxCount(4)).Wait();

			res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Select(x => x.Event.EventId).ToArray());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void more_strict_max_count_gives_less_events_for_stream_forward_read() {
			const string stream = "more_strict_max_count_gives_less_events_for_stream_forward_read";

			_conn.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, _testEvents).Wait();

			_conn.SetStreamMetadataAsync(stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetTruncateBefore(2)).Wait();

			var res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Select(x => x.Event.EventId).ToArray());

			_conn.SetStreamMetadataAsync(stream, 0, StreamMetadata.Build().SetTruncateBefore(2).SetMaxCount(2)).Wait();

			res = _conn.ReadStreamEventsForwardAsync(stream, 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(2, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(3).Select(x => x.EventId).ToArray(),
				res.Events.Select(x => x.Event.EventId).ToArray());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void after_setting_less_strict_truncatebefore_read_stream_backward_reads_more_events() {
			const string stream = "after_setting_less_strict_truncatebefore_read_stream_backward_reads_more_events";

			_conn.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, _testEvents).Wait();

			_conn.SetStreamMetadataAsync(stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetTruncateBefore(2)).Wait();

			var res = _conn.ReadStreamEventsBackwardAsync(stream, -1, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Reverse().Select(x => x.Event.EventId).ToArray());

			_conn.SetStreamMetadataAsync(stream, 0, StreamMetadata.Build().SetTruncateBefore(1)).Wait();

			res = _conn.ReadStreamEventsBackwardAsync(stream, -1, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(4, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(1).Select(x => x.EventId).ToArray(),
				res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void after_setting_more_strict_truncatebefore_read_stream_backward_reads_less_events() {
			const string stream = "after_setting_more_strict_truncatebefore_read_stream_backward_reads_less_events";

			_conn.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, _testEvents).Wait();

			_conn.SetStreamMetadataAsync(stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetTruncateBefore(2)).Wait();

			var res = _conn.ReadStreamEventsBackwardAsync(stream, -1, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Reverse().Select(x => x.Event.EventId).ToArray());

			_conn.SetStreamMetadataAsync(stream, 0, StreamMetadata.Build().SetTruncateBefore(3)).Wait();

			res = _conn.ReadStreamEventsBackwardAsync(stream, -1, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(2, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(3).Select(x => x.EventId).ToArray(),
				res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void less_strict_max_count_doesnt_change_anything_for_stream_backward_read() {
			const string stream = "less_strict_max_count_doesnt_change_anything_for_stream_backward_read";

			_conn.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, _testEvents).Wait();

			_conn.SetStreamMetadataAsync(stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetTruncateBefore(2)).Wait();

			var res = _conn.ReadStreamEventsBackwardAsync(stream, -1, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Reverse().Select(x => x.Event.EventId).ToArray());

			_conn.SetStreamMetadataAsync(stream, 0, StreamMetadata.Build().SetTruncateBefore(2).SetMaxCount(4)).Wait();

			res = _conn.ReadStreamEventsBackwardAsync(stream, -1, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
		}

		[Test, Category("LongRunning"), Category("Network")]
		public void more_strict_max_count_gives_less_events_for_stream_backward_read() {
			const string stream = "more_strict_max_count_gives_less_events_for_stream_backward_read";

			_conn.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, _testEvents).Wait();

			_conn.SetStreamMetadataAsync(stream, ExpectedVersion.EmptyStream,
				StreamMetadata.Build().SetTruncateBefore(2)).Wait();

			var res = _conn.ReadStreamEventsBackwardAsync(stream, -1, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(3, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(2).Select(x => x.EventId).ToArray(),
				res.Events.Reverse().Select(x => x.Event.EventId).ToArray());

			_conn.SetStreamMetadataAsync(stream, 0, StreamMetadata.Build().SetTruncateBefore(2).SetMaxCount(2)).Wait();

			res = _conn.ReadStreamEventsBackwardAsync(stream, -1, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.Success, res.Status);
			Assert.AreEqual(2, res.Events.Length);
			Assert.AreEqual(_testEvents.Skip(3).Select(x => x.EventId).ToArray(),
				res.Events.Reverse().Select(x => x.Event.EventId).ToArray());
		}
	}
}
