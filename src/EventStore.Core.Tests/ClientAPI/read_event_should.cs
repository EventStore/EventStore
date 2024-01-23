extern alias GrpcClient;
using EventData = GrpcClient::EventStore.Client.EventData;
using Uuid = GrpcClient::EventStore.Client.Uuid;
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using EventStore.Core.Tests.ClientAPI.Helpers;

namespace EventStore.Core.Tests.ClientAPI {
	[Category("ClientAPI"), Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class read_event_should<TLogFormat, TStreamId>
		: SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private Uuid _eventId0;
		private Uuid _eventId1;

		protected override async Task When() {
			_eventId0 = Uuid.NewUuid();
			_eventId1 = Uuid.NewUuid();

			await _conn.AppendToStreamAsync("test-stream",
					-1,
					new EventData(_eventId0, "event0", new byte[3], new byte[2]),
					new EventData(_eventId1, "event1", new byte[7], new byte[10]));
			await _conn.DeleteStreamAsync("deleted-stream", -1, hardDelete: true);
		}

		[Test, Category("Network")]
		public async Task throw_if_stream_id_is_null() {
			await AssertEx.ThrowsAsync<ArgumentNullException>(() => _conn.ReadEventAsync(null, 0, resolveLinkTos: false));
		}

		[Test, Category("Network")]
		public async Task throw_if_stream_id_is_empty() {
			await AssertEx.ThrowsAsync<ArgumentNullException>(() => _conn.ReadEventAsync("", 0, resolveLinkTos: false));
		}

		[Test, Category("Network")]
		public async Task throw_if_event_number_is_less_than_minus_one() {
			await AssertEx.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				_conn.ReadEventAsync("stream", -2, resolveLinkTos: false));
		}

		[Test, Category("Network")]
		public async Task notify_using_status_code_if_stream_not_found() {
			var res = await _conn.ReadEventAsync("unexisting-stream", 5, false);

			Assert.AreEqual(EventReadStatus.NotFound, res.Status);
			Assert.IsNull(res.Event);
			Assert.AreEqual("unexisting-stream", res.Stream);
			Assert.AreEqual(5, res.EventNumber);
		}

		[Test, Category("Network")]
		public async Task return_no_stream_if_requested_last_event_in_empty_stream() {
			var res = await _conn.ReadEventAsync("some-really-empty-stream", -1, false);
			Assert.AreEqual(EventReadStatus.NoStream, res.Status);
		}

		[Test, Category("Network")]
		public async Task notify_using_status_code_if_stream_was_deleted() {
			var res = await _conn.ReadEventAsync("deleted-stream", 5, false);

			Assert.AreEqual(EventReadStatus.StreamDeleted, res.Status);
			Assert.IsNull(res.Event);
			Assert.AreEqual("deleted-stream", res.Stream);
			Assert.AreEqual(5, res.EventNumber);
		}

		[Test, Category("Network")]
		public async Task notify_using_status_code_if_stream_does_not_have_event() {
			var res = await _conn.ReadEventAsync("test-stream", 5, false);

			Assert.AreEqual(EventReadStatus.NotFound, res.Status);
			Assert.IsNull(res.Event);
			Assert.AreEqual("test-stream", res.Stream);
			Assert.AreEqual(5, res.EventNumber);
		}

		[Test, Category("Network")]
		public async Task return_existing_event() {
			var res = await _conn.ReadEventAsync("test-stream", 0, false);

			Assert.AreEqual(EventReadStatus.Success, res.Status);
			Assert.AreEqual(res.Event.Value.OriginalEvent.EventId, _eventId0);
			Assert.AreEqual("test-stream", res.Stream);
			Assert.AreEqual(0, res.EventNumber);
			Assert.AreNotEqual(DateTime.MinValue, res.Event.Value.OriginalEvent.Created);
			Assert.AreNotEqual(0, res.Event.Value.OriginalEvent.Created.ToEpoch());
		}

		[Test, Category("Network")]
		public async Task retrieve_the_is_json_flag_properly() {
			var res = await _conn.ReadEventAsync("test-stream", 1, false);

			Assert.AreEqual(EventReadStatus.Success, res.Status);
			Assert.AreEqual(res.Event.Value.OriginalEvent.EventId, _eventId1);
			Assert.AreEqual("application/json", res.Event.Value.OriginalEvent.ContentType);
		}

		[Test, Category("Network")]
		public async Task return_last_event_in_stream_if_event_number_is_minus_one() {
			var res = await _conn.ReadEventAsync("test-stream", -1, false);

			Assert.AreEqual(EventReadStatus.Success, res.Status);
			Assert.AreEqual(res.Event.Value.OriginalEvent.EventId, _eventId1);
			Assert.AreEqual("test-stream", res.Stream);
			Assert.AreEqual(-1, res.EventNumber);
			Assert.AreNotEqual(DateTime.MinValue, res.Event.Value.OriginalEvent.Created);
			Assert.AreNotEqual(0, res.Event.Value.OriginalEvent.Created.ToEpoch());
		}
	}
}
