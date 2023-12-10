extern alias GrpcClient;
extern alias GrpcClientStreams;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EventStore.Core.Tests.ClientAPI.Helpers;
using GrpcClient::EventStore.Client;
using NUnit.Framework;
using Direction = GrpcClientStreams::EventStore.Client.Direction;
using StreamAcl = GrpcClientStreams::EventStore.Client.StreamAcl;
using StreamMetadata = GrpcClientStreams::EventStore.Client.StreamMetadata;
using SystemRoles = EventStore.Core.Services.SystemRoles;

namespace EventStore.Core.Tests.ClientAPI {

	[Category("ClientAPI"), Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class read_all_events_backward_filtered_should<TLogFormat, TStreamId>
		: SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private List<EventData> _testEvents;

		protected override async Task When() {
			await _conn.SetStreamMetadataAsync("$all", -1,
					new StreamMetadata(acl: new StreamAcl(readRole: SystemRoles.All)),
					DefaultData.AdminCredentials);

			_testEvents = Enumerable.Range(0, 10).Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "AEvent"))
				.ToList();
			_testEvents.AddRange(
				Enumerable.Range(0, 10).Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "BEvent"))
					.ToList());

			await _conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _testEvents.EvenEvents());
			await _conn.AppendToStreamAsync("stream-b", ExpectedVersion.NoStream, _testEvents.OddEvents());
		}

		[Test, Category("LongRunning")]
		public async Task only_return_events_with_a_given_stream_prefix() {
			var filter = Filter.StreamId.Prefix("stream-a");

			var read = await _conn.FilteredReadAllEventsBackwardAsync(Position.End, 4096, false, filter, 4096);
			Assert.That(EventDataComparer.Equal(
				_testEvents.EvenEvents().ReverseEvents(),
				read.Events.Select(x => x.Event).ToArray()));
		}

		[Test, Category("LongRunning")]
		public async Task only_return_events_with_a_given_event_prefix() {
			var filter = Filter.EventType.Prefix("AE");

			// Have to order the events as we are writing to two streams and can't guarantee ordering

			var read = await _conn.FilteredReadAllEventsBackwardAsync(Position.End, 4096, false, filter, 4096);
			Assert.AreEqual(Direction.Backwards, read.ReadDirection);
			Assert.That(EventDataComparer.Equal(
				_testEvents.Where(e => e.Type == "AEvent").OrderBy(x => x.EventId.ToGuid()).ToArray(),
				read.Events.Select(x => x.Event).OrderBy(x => x.EventId.ToGuid()).ToArray()));
		}

		[Test, Category("LongRunning")]
		public async Task only_return_events_that_satisfy_a_given_stream_regex() {
			var filter = Filter.StreamId.Regex(new Regex(@"^.*m-b.*$"));

			var read = await _conn.FilteredReadAllEventsBackwardAsync(Position.End, 4096, false, filter, 4096);
			Assert.AreEqual(Direction.Backwards, read.ReadDirection);
			Assert.That(EventDataComparer.Equal(
				_testEvents.OddEvents().ReverseEvents(),
				read.Events.Select(x => x.Event).ToArray()));
		}

		[Test, Category("LongRunning")]
		public async Task only_return_events_that_satisfy_a_given_event_regex() {
			var filter = Filter.EventType.Regex(new Regex(@"^.*BEv.*$"));

			// Have to order the events as we are writing to two streams and can't guarantee ordering
			var read = await _conn.FilteredReadAllEventsBackwardAsync(Position.End, 4096, false, filter, 4096);
			Assert.AreEqual(Direction.Backwards, read.ReadDirection);
			Assert.That(EventDataComparer.Equal(
				_testEvents.Where(e => e.Type == "BEvent").OrderBy(x => x.EventId.ToGuid()).ToArray(),
				read.Events.Select(x => x.Event).OrderBy(x => x.EventId.ToGuid()).ToArray()));
		}

		[Test, Category("LongRunning")]
		public async Task only_return_events_that_are_not_system_events() {
			var filter = Filter.ExcludeSystemEvents;

			// Have to order the events as we are writing to two streams and can't guarantee ordering
			var read = await _conn.FilteredReadAllEventsBackwardAsync(Position.End, 4096, false, filter, 4096);
			Assert.AreEqual(Direction.Backwards, read.ReadDirection);
			Assert.That(!read.Events.Any(e => e.Event.EventType.StartsWith("$")));
		}
	}
}
