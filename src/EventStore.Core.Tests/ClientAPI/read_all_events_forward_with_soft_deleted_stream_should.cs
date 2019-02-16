using System.Collections.Generic;
using System.Linq;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;
using ExpectedVersion = EventStore.ClientAPI.ExpectedVersion;
using StreamMetadata = EventStore.ClientAPI.StreamMetadata;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class read_all_events_forward_with_soft_deleted_stream_should : SpecificationWithMiniNode {
		private EventData[] _testEvents;

		protected override void When() {
			_conn.SetStreamMetadataAsync(
					"$all", -1, StreamMetadata.Build().SetReadRole(SystemRoles.All),
					new UserCredentials(SystemUsers.Admin, SystemUsers.DefaultAdminPassword))
				.Wait();

			_testEvents = Enumerable.Range(0, 20).Select(x => TestEvent.NewTestEvent(x.ToString())).ToArray();
			_conn.AppendToStreamAsync("stream", ExpectedVersion.EmptyStream, _testEvents).Wait();
			_conn.DeleteStreamAsync("stream", ExpectedVersion.Any).Wait();
		}

		[Test, Category("LongRunning")]
		public void ensure_deleted_stream() {
			var res = _conn.ReadStreamEventsForwardAsync("stream", 0, 100, false).Result;
			Assert.AreEqual(SliceReadStatus.StreamNotFound, res.Status);
			Assert.AreEqual(0, res.Events.Length);
		}

		[Test, Category("LongRunning")]
		public void returns_all_events_including_tombstone() {
			AllEventsSlice read = _conn.ReadAllEventsForwardAsync(Position.Start, _testEvents.Length + 10, false)
				.Result;
			Assert.That(
				EventDataComparer.Equal(
					_testEvents.ToArray(),
					read.Events.Skip(read.Events.Length - _testEvents.Length - 1)
						.Take(_testEvents.Length)
						.Select(x => x.Event)
						.ToArray()));
			var lastEvent = read.Events.Last().Event;
			Assert.AreEqual("$$stream", lastEvent.EventStreamId);
			Assert.AreEqual(SystemEventTypes.StreamMetadata, lastEvent.EventType);
			var metadata = StreamMetadata.FromJsonBytes(lastEvent.Data);
			Assert.AreEqual(EventNumber.DeletedStream, metadata.TruncateBefore);
		}
	}
}
