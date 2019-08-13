using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EventStore.ClientAPI;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;
using ExpectedVersion = EventStore.ClientAPI.ExpectedVersion;
using ResolvedEvent = EventStore.ClientAPI.ResolvedEvent;
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

			_testEvents = Enumerable.Range(0, 10).Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "AEvent"))
				.ToList();
			_testEvents.AddRange(
				Enumerable.Range(0, 10).Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "BEvent"))
					.ToList());

			_conn.AppendToStreamAsync("stream-a", ExpectedVersion.EmptyStream, _testEvents.EvenEvents()).Wait();
			_conn.AppendToStreamAsync("stream-b", ExpectedVersion.EmptyStream, _testEvents.OddEvents()).Wait();
		}

		[Test, Category("LongRunning")]
		public void only_return_events_with_a_given_stream_prefix() {
			var filter = EventFilter
				.Create()
				.WithStreamPrefixFilter("stream-a")
				.Build();

			var read = _conn.ReadAllEventsForwardFilteredAsync(Position.Start, 1000, false, filter).Result;
			Assert.That(EventDataComparer.Equal(
				_testEvents.EvenEvents().ToArray(),
				read.Events.Select(x => x.Event).ToArray()));
		}

		[Test, Category("LongRunning")]
		public void only_return_events_with_a_given_event_prefix() {
			var filter = EventFilter
				.Create()
				.WithEventPrefixFilter("AE")
				.Build();

			// Have to order the events as we are writing to two streams and can't guarantee ordering
			var read = _conn.ReadAllEventsForwardFilteredAsync(Position.Start, 1000, false, filter).Result;
			Assert.That(EventDataComparer.Equal(
				_testEvents.Where(e => e.Type == "AEvent").OrderBy(x => x.EventId).ToArray(),
				read.Events.Select(x => x.Event).OrderBy(x => x.EventId).ToArray()));
		}

		[Test, Category("LongRunning")]
		public void only_return_events_that_satisfy_a_given_stream_regex() {
			var filter = EventFilter
				.Create()
				.WithStreamFilter(new Regex(@"^.*m-b.*$"))
				.Build();

			var read = _conn.ReadAllEventsForwardFilteredAsync(Position.Start, 1000, false, filter).Result;
			Assert.That(EventDataComparer.Equal(
				_testEvents.OddEvents().ToArray(),
				read.Events.Select(x => x.Event).ToArray()));
		}

		[Test, Category("LongRunning")]
		public void only_return_events_that_satisfy_a_given_event_regex() {
			var filter = EventFilter
				.Create()
				.WithEventFilter(new Regex(@"^.*BEv.*$"))
				.Build();

			// Have to order the events as we are writing to two streams and can't guarantee ordering
			var read = _conn.ReadAllEventsForwardFilteredAsync(Position.Start, 1000, false, filter).Result;
			Assert.That(EventDataComparer.Equal(
				_testEvents.Where(e => e.Type == "BEvent").OrderBy(x => x.EventId).ToArray(),
				read.Events.Select(x => x.Event).OrderBy(x => x.EventId).ToArray()));
		}

		[Test, Category("LongRunning")]
		public void only_return_events_that_are_not_system_events() {
			var filter = EventFilter
				.Create()
				.ExcludeSystemEvents()
				.Build();

			// Have to order the events as we are writing to two streams and can't guarantee ordering
			var read = _conn.ReadAllEventsForwardFilteredAsync(Position.Start, 1000, false, filter).Result;
			Assert.That(!read.Events.Any(e => e.Event.EventType.StartsWith("$")));
		}

		[Test, Category("LongRunning")]
		public void handle_long_gaps_between_events() {
			var aEvents = Enumerable.Range(0, 10000)
				.Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "AEvent"))
				.ToList();
			var cEvents = Enumerable.Range(0, 10).Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "CEvent"))
				.ToList();

			_conn.AppendToStreamAsync("stream-longgap", ExpectedVersion.Any, aEvents).Wait();
			_conn.AppendToStreamAsync("stream-longgap", ExpectedVersion.Any, cEvents).Wait();

			var filter = EventFilter
				.Create()
				.WithEventPrefixFilter("CE")
				.Build();

			var sliceStart = Position.Start;
			var read = new List<ResolvedEvent>();
			AllEventsSlice slice;

			do {
				slice = _conn.ReadAllEventsForwardFilteredAsync(sliceStart, 4096, false, filter, maxSearchWindow: 4096)
					.GetAwaiter()
					.GetResult();
				read.AddRange(slice.Events);
				sliceStart = slice.NextPosition;
			} while (!slice.IsEndOfStream);

			Assert.That(EventDataComparer.Equal(
				cEvents.ToArray(),
				read.Select(x => x.Event).ToArray()));
		}
	}
}
