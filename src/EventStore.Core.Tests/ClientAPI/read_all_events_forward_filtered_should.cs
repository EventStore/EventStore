using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EventStore.ClientAPI;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;
using ExpectedVersion = EventStore.ClientAPI.ExpectedVersion;
using StreamMetadata = EventStore.ClientAPI.StreamMetadata;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class read_all_events_forward_filtered_should : SpecificationWithMiniNode {
		private List<EventData> _testEvents;

		protected override void When() {
			_conn.SetStreamMetadataAsync("$all", -1,
					StreamMetadata.Build().SetReadRole(SystemRoles.All),
					DefaultData.AdminCredentials)
				.Wait();

			_testEvents = Enumerable
				.Range(0, 10)
				.Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "AEvent"))
				.ToList();

			_testEvents.AddRange(
				Enumerable
					.Range(0, 10)
					.Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "BEvent")).ToList());

			_conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _testEvents.EvenEvents()).Wait();
			_conn.AppendToStreamAsync("stream-b", ExpectedVersion.NoStream, _testEvents.OddEvents()).Wait();
		}

		[Test, Category("LongRunning")]
		public void only_return_events_with_a_given_stream_prefix() {
			var filter = Filter.StreamId.Prefix("stream-a");

			var read = _conn.FilteredReadAllEventsForwardAsync(Position.Start, 1000, false, filter, 1000).Result;
			Assert.That(EventDataComparer.Equal(
				_testEvents.EvenEvents().ToArray(),
				read.Events.Select(x => x.Event).ToArray()));
		}

		[Test, Category("LongRunning")]
		public void only_return_events_with_a_given_event_prefix() {
			var filter = Filter.EventType.Prefix("AE");

			// Have to order the events as we are writing to two streams and can't guarantee ordering
			var read = _conn.FilteredReadAllEventsForwardAsync(Position.Start, 1000, false, filter, 1000).Result;
			Assert.That(EventDataComparer.Equal(
				_testEvents.Where(e => e.Type == "AEvent").OrderBy(x => x.EventId).ToArray(),
				read.Events.Select(x => x.Event).OrderBy(x => x.EventId).ToArray()));
		}

		[Test, Category("LongRunning")]
		public void only_return_events_that_satisfy_a_given_stream_regex() {
			var filter = Filter.StreamId.Regex(new Regex(@"^.*eam-b.*$"));

			var read = _conn.FilteredReadAllEventsForwardAsync(Position.Start, 1000, false, filter, 1000).Result;
			Assert.That(EventDataComparer.Equal(
				_testEvents.OddEvents().ToArray(),
				read.Events.Select(x => x.Event).ToArray()));
		}

		[Test, Category("LongRunning")]
		public void only_return_events_that_satisfy_a_given_event_regex() {
			var filter = Filter.EventType.Regex(new Regex(@"^.*BEv.*$"));

			// Have to order the events as we are writing to two streams and can't guarantee ordering
			var read = _conn.FilteredReadAllEventsForwardAsync(Position.Start, 1000, false, filter, 1000).Result;
			Assert.That(EventDataComparer.Equal(
				_testEvents.Where(e => e.Type == "BEvent").OrderBy(x => x.EventId).ToArray(),
				read.Events.Select(x => x.Event).OrderBy(x => x.EventId).ToArray()));
		}

		[Test, Category("LongRunning")]
		public void only_return_events_that_are_not_system_events() {
			var filter = Filter.ExcludeSystemEvents;

			// Have to order the events as we are writing to two streams and can't guarantee ordering
			var read = _conn.FilteredReadAllEventsForwardAsync(Position.Start, 1000, false, filter, 1000).Result;
			Assert.That(!read.Events.Any(e => e.Event.EventType.StartsWith("$")));
		}
	}

	internal static class EventDataExtensions {
		internal static List<EventData> EvenEvents(this List<EventData> ed) => ed.Where((c, i) => i % 2 == 0).ToList();

		internal static List<EventData> OddEvents(this List<EventData> ed) => ed.Where((c, i) => i % 2 != 0).ToList();

		internal static EventData[] ReverseEvents(this List<EventData> ed) => ed.ToArray().Reverse().ToArray();
	}
}
