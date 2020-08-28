using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.Services;
using NUnit.Framework;
using ExpectedVersion = EventStore.ClientAPI.ExpectedVersion;
using StreamMetadata = EventStore.ClientAPI.StreamMetadata;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class read_all_events_forward_with_hard_deleted_stream_should : SpecificationWithMiniNode {
		private EventData[] _testEvents;

		protected override async Task When() {
			await _conn.SetStreamMetadataAsync(
					"$all", -1, StreamMetadata.Build().SetReadRole(SystemRoles.All),
					new UserCredentials(SystemUsers.Admin, SystemUsers.DefaultAdminPassword));

			_testEvents = Enumerable.Range(0, 20).Select(x => TestEvent.NewTestEvent(x.ToString())).ToArray();
			await _conn.AppendToStreamAsync("stream", ExpectedVersion.NoStream, _testEvents);
			await _conn.DeleteStreamAsync("stream", ExpectedVersion.Any, hardDelete: true);
		}

		[Test, Category("LongRunning")]
		public async Task ensure_deleted_stream() {
			var res = await _conn.ReadStreamEventsForwardAsync("stream", 0, 100, false);
			Assert.AreEqual(SliceReadStatus.StreamDeleted, res.Status);
			Assert.AreEqual(0, res.Events.Length);
		}

		[Test, Category("LongRunning")]
		public async Task returns_all_events_including_tombstone() {
			AllEventsSlice read = await _conn.ReadAllEventsForwardAsync(Position.Start, _testEvents.Length + 10, false)
;
			Assert.That(
				EventDataComparer.Equal(
					_testEvents.ToArray(),
					read.Events.Skip(read.Events.Length - _testEvents.Length - 1)
						.Take(_testEvents.Length)
						.Select(x => x.Event)
						.ToArray()));
			var lastEvent = read.Events.Last().Event;
			Assert.AreEqual("stream", lastEvent.EventStreamId);
			Assert.AreEqual(SystemEventTypes.StreamDeleted, lastEvent.EventType);
		}
	}
}
