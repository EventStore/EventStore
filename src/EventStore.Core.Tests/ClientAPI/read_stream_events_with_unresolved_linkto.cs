extern alias GrpcClient;
extern alias GrpcClientStreams;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using GrpcClient::EventStore.Client;
using NUnit.Framework;
using StreamAcl = GrpcClientStreams::EventStore.Client.StreamAcl;
using StreamMetadata = GrpcClientStreams::EventStore.Client.StreamMetadata;
using SystemRoles = EventStore.Core.Services.SystemRoles;

namespace EventStore.Core.Tests.ClientAPI {
	[Category("ClientAPI"), Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class read_stream_events_with_unresolved_linkto<TLogFormat, TStreamId> : SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private EventData[] _testEvents;

		protected override async Task When() {
			await _conn.SetStreamMetadataAsync(
					"$all", -1, new StreamMetadata(acl: new StreamAcl(readRole: SystemRoles.All)),
					new UserCredentials(SystemUsers.Admin, SystemUsers.DefaultAdminPassword));

			_testEvents = Enumerable.Range(0, 20).Select(x => TestEvent.NewTestEvent(x.ToString())).ToArray();
			await _conn.AppendToStreamAsync("stream", ExpectedVersion.NoStream, _testEvents);
			await _conn.AppendToStreamAsync(
					"links", ExpectedVersion.NoStream,
					new EventData(
						Uuid.NewUuid(), EventStore.ClientAPI.Common.SystemEventTypes.LinkTo,
						Encoding.UTF8.GetBytes("0@stream"), null));
			await _conn.DeleteStreamAsync("stream", ExpectedVersion.Any);
		}

		[Test, Category("LongRunning")]
		public async Task ensure_deleted_stream() {
			var res = await _conn.ReadStreamEventsForwardAsync("stream", 0, 100, false);
			Assert.AreEqual(SliceReadStatus.StreamNotFound, res.Status);
			Assert.AreEqual(0, res.Events.Length);
		}

		[Test, Category("LongRunning")]
		public async Task returns_unresolved_linkto() {
			var read = await _conn.ReadStreamEventsForwardAsync("links", 0, 1, true);
			Assert.AreEqual(1, read.Events.Length);
			Assert.IsNull(read.Events[0].Event);
			Assert.IsNotNull(read.Events[0].Link);
		}
	}
}
