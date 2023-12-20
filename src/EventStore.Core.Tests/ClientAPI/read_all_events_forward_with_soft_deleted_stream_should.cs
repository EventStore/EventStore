extern alias GrpcClient;
extern alias GrpcClientStreams;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using GrpcClient::EventStore.Client;
using NUnit.Framework;
using ExpectedVersion = EventStore.Core.Tests.ClientAPI.Helpers.ExpectedVersion;
using StreamAcl = GrpcClientStreams::EventStore.Client.StreamAcl;
using StreamMetadata = GrpcClientStreams::EventStore.Client.StreamMetadata;
using SystemRoles = EventStore.Core.Services.SystemRoles;

namespace EventStore.Core.Tests.ClientAPI {
	[Category("ClientAPI"), Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class read_all_events_forward_with_soft_deleted_stream_should<TLogFormat, TStreamId>
		: SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private EventData[] _testEvents;

		protected override async Task When() {
			await _conn.SetStreamMetadataAsync(
					"$all", -1, new StreamMetadata(acl: new StreamAcl(readRole: SystemRoles.All)),
					new UserCredentials(SystemUsers.Admin, SystemUsers.DefaultAdminPassword));

			_testEvents = Enumerable.Range(0, 20).Select(x => TestEvent.NewTestEvent(x.ToString())).ToArray();
			await _conn.AppendToStreamAsync("stream", ExpectedVersion.NoStream, _testEvents);
			await _conn.DeleteStreamAsync("stream", ExpectedVersion.Any);
		}

		[Test, Category("LongRunning")]
		public async Task ensure_deleted_stream() {
			var res = await _conn.ReadStreamEventsForwardAsync("stream", 0, 100, false);
			Assert.AreEqual(SliceReadStatus.StreamNotFound, res.Status);
			Assert.AreEqual(0, res.Events.Length);
		}

		[Test, Category("LongRunning")]
		public async Task returns_all_events_including_tombstone() {
			AllEventsSliceNew read = await _conn.ReadAllEventsForwardAsync(Position.Start, _testEvents.Length + 20, false)
;
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
			var document = JsonDocument.Parse(Encoding.UTF8.GetString(lastEvent.Data.ToArray()));
			var tb = document.RootElement.GetProperty("$tb").GetInt64();
			Assert.AreEqual(tb, EventNumber.DeletedStream);
			var meta = await _conn.GetStreamMetadataAsync("stream");
			Assert.AreEqual(meta.Metadata.TruncateBefore, StreamPosition.End);
		}
	}
}
