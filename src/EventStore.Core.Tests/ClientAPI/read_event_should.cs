using System;
using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class read_event_should : SpecificationWithMiniNode {
		private Guid _eventId0;
		private Guid _eventId1;

		protected override void When() {
			_eventId0 = Guid.NewGuid();
			_eventId1 = Guid.NewGuid();

			_conn.AppendToStreamAsync("test-stream",
					-1,
					new EventData(_eventId0, "event0", false, new byte[3], new byte[2]),
					new EventData(_eventId1, "event1", true, new byte[7], new byte[10]))
				.Wait();
			_conn.DeleteStreamAsync("deleted-stream", -1, hardDelete: true).Wait();
		}

		[Test, Category("Network")]
		public void throw_if_stream_id_is_null() {
			Assert.ThrowsAsync<ArgumentNullException>(() => _conn.ReadEventAsync(null, 0, resolveLinkTos: false));
		}

		[Test, Category("Network")]
		public void throw_if_stream_id_is_empty() {
			Assert.ThrowsAsync<ArgumentNullException>(() => _conn.ReadEventAsync("", 0, resolveLinkTos: false));
		}

		[Test, Category("Network")]
		public void throw_if_event_number_is_less_than_minus_one() {
			Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				_conn.ReadEventAsync("stream", -2, resolveLinkTos: false));
		}

		[Test, Category("Network")]
		public void notify_using_status_code_if_stream_not_found() {
			var res = _conn.ReadEventAsync("unexisting-stream", 5, false).Result;

			Assert.AreEqual(EventReadStatus.NoStream, res.Status);
			Assert.IsNull(res.Event);
			Assert.AreEqual("unexisting-stream", res.Stream);
			Assert.AreEqual(5, res.EventNumber);
		}

		[Test, Category("Network")]
		public void return_no_stream_if_requested_last_event_in_empty_stream() {
			var res = _conn.ReadEventAsync("some-really-empty-stream", -1, false).Result;
			Assert.AreEqual(EventReadStatus.NoStream, res.Status);
		}

		[Test, Category("Network")]
		public void notify_using_status_code_if_stream_was_deleted() {
			var res = _conn.ReadEventAsync("deleted-stream", 5, false).Result;

			Assert.AreEqual(EventReadStatus.StreamDeleted, res.Status);
			Assert.IsNull(res.Event);
			Assert.AreEqual("deleted-stream", res.Stream);
			Assert.AreEqual(5, res.EventNumber);
		}

		[Test, Category("Network")]
		public void notify_using_status_code_if_stream_does_not_have_event() {
			var res = _conn.ReadEventAsync("test-stream", 5, false).Result;

			Assert.AreEqual(EventReadStatus.NotFound, res.Status);
			Assert.IsNull(res.Event);
			Assert.AreEqual("test-stream", res.Stream);
			Assert.AreEqual(5, res.EventNumber);
		}

		[Test, Category("Network")]
		public void return_existing_event() {
			var res = _conn.ReadEventAsync("test-stream", 0, false).Result;

			Assert.AreEqual(EventReadStatus.Success, res.Status);
			Assert.AreEqual(res.Event.Value.OriginalEvent.EventId, _eventId0);
			Assert.AreEqual("test-stream", res.Stream);
			Assert.AreEqual(0, res.EventNumber);
			Assert.AreNotEqual(DateTime.MinValue, res.Event.Value.OriginalEvent.Created);
			Assert.AreNotEqual(0, res.Event.Value.OriginalEvent.CreatedEpoch);
		}

		[Test, Category("Network")]
		public void retrieve_the_is_json_flag_properly() {
			var res = _conn.ReadEventAsync("test-stream", 1, false).Result;

			Assert.AreEqual(EventReadStatus.Success, res.Status);
			Assert.AreEqual(res.Event.Value.OriginalEvent.EventId, _eventId1);
			Assert.IsTrue(res.Event.Value.OriginalEvent.IsJson);
		}

		[Test, Category("Network")]
		public void return_last_event_in_stream_if_event_number_is_minus_one() {
			var res = _conn.ReadEventAsync("test-stream", -1, false).Result;

			Assert.AreEqual(EventReadStatus.Success, res.Status);
			Assert.AreEqual(res.Event.Value.OriginalEvent.EventId, _eventId1);
			Assert.AreEqual("test-stream", res.Stream);
			Assert.AreEqual(-1, res.EventNumber);
			Assert.AreNotEqual(DateTime.MinValue, res.Event.Value.OriginalEvent.Created);
			Assert.AreNotEqual(0, res.Event.Value.OriginalEvent.CreatedEpoch);
		}
	}
}
