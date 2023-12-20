extern alias GrpcClient;
extern alias GrpcClientStreams;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
	public class read_all_events_filtered_paging_should<TLogFormat, TStreamId>
		: SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private List<EventData> _testEvents = new List<EventData>();

		private List<EventData> _testEventsA;

		private List<EventData> _testEventsC;

		public read_all_events_filtered_paging_should() : base(chunkSize: 2 * 1024 * 1024) { }

		protected override async Task When() {
			await _conn.SetStreamMetadataAsync("$all", -1,
				new StreamMetadata(acl: new StreamAcl(readRole: SystemRoles.All)),
				DefaultData.AdminCredentials);

			_testEventsA = Enumerable
				.Range(0, 10)
				.Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "AEvent"))
				.ToList();

			var testEventsB = Enumerable
				.Range(0, 10000)
				.Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "BEvent"))
				.ToList();

			_testEventsC = Enumerable
				.Range(0, 10)
				.Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "CEvent"))
				.ToList();

			_testEvents.AddRange(_testEventsA);
			_testEvents.AddRange(testEventsB);
			_testEvents.AddRange(_testEventsC);

			_conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _testEvents).Wait();
		}

		[Test, Category("LongRunning")]
		public async Task handle_paging_between_events_forward() {
			var numberOfEmptySlicesRead = 0;

			var filter = Filter.EventType.Prefix("CE");
			var sliceStart = Position.Start;
			var read = new List<ResolvedEvent>();
			AllEventsSliceNew slice;

			do {
				slice = await _conn.FilteredReadAllEventsForwardAsync(sliceStart, 50, false, filter, maxSearchWindow: 100);

				if (slice.Events.Length == 0) {
					numberOfEmptySlicesRead++;
				} else {
					read.AddRange(slice.Events);
				}

				sliceStart = slice.NextPosition;
			} while (!slice.IsEndOfStream);

			Assert.That(EventDataComparer.Equal(
				_testEventsC.ToArray(),
				read.Select(x => x.Event).ToArray()));

			Assert.AreEqual(100, numberOfEmptySlicesRead);
		}

		[Test, Category("LongRunning")]
		public async Task handle_paging_between_events_backward() {
			var numberOfEmptySlicesRead = 0;

			var filter = Filter.EventType.Prefix("AE");
			var sliceStart = Position.End;
			var read = new List<ResolvedEvent>();
			AllEventsSliceNew slice;

			do {
				slice = await _conn.FilteredReadAllEventsBackwardAsync(sliceStart, 50, false, filter, maxSearchWindow: 100);
				if (slice.Events.Length == 0) {
					numberOfEmptySlicesRead++;
				} else {
					read.AddRange(slice.Events);
				}

				sliceStart = slice.NextPosition;
			} while (!slice.IsEndOfStream);

			Assert.That(EventDataComparer.Equal(
				_testEventsA.ReverseEvents(),
				read.Select(x => x.Event).ToArray()));

			Assert.AreEqual(100, numberOfEmptySlicesRead);
		}

		[Test, Category("LongRunning")]
		public async Task handle_paging_between_events_returns_correct_number_of_events_for_max_search_window_forward() {
			var filter = Filter.EventType.Prefix("BE");

			var slice = await _conn.FilteredReadAllEventsForwardAsync(Position.Start, 30, false, filter, maxSearchWindow: 30);

			// Includes system events at start of stream (inc epoch-information)
			// Old test had 11 expected events, now we have 12. This test is useless.
			// var expectedCount = 11;
			var expectedCount = 12;

			if (LogFormatHelper<TLogFormat, TStreamId>.IsV3) {
				// account for stream records: $scavenges, $user-admin, $user-ops, $users, stream-a
				expectedCount -= 5;
				// account for event type records: $UserCreated, $User, AEvent, BEvent, CEvent
				expectedCount -= 5;
			}

			Assert.AreEqual(expectedCount, slice.Events.Length);
		}

		[Test, Category("LongRunning")]
		public async Task handle_paging_between_events_returns_correct_number_of_events_for_max_search_window_backward() {
			var filter = Filter.EventType.Prefix("BE");

			var slice = await _conn.FilteredReadAllEventsBackwardAsync(Position.End, 20, false, filter, maxSearchWindow: 20);

			Assert.AreEqual(10, slice.Events.Length); // Includes system events
		}
	}
}
