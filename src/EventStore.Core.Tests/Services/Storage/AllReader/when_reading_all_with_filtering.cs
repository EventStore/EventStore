using System;
using NUnit.Framework;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLogV2.Data;

namespace EventStore.Core.Tests.Services.Storage.AllReader {
	public class when_reading_all_with_filtering : ReadIndexTestScenario {
		TFPos _forwardReadPos;
		TFPos _backwardReadPos;

		protected override void WriteTestScenario() {
			var firstEvent = WriteSingleEvent("ES1", 1, new string('.', 3000), eventId: Guid.NewGuid(),
				eventType: "event-type-1", retryOnFail: true);
			WriteSingleEvent("ES2", 1, new string('.', 3000), eventId: Guid.NewGuid(), eventType: "other-event-type-2",
				retryOnFail: true);
			WriteSingleEvent("ES3", 1, new string('.', 3000), eventId: Guid.NewGuid(), eventType: "event-type-3",
				retryOnFail: true);
			WriteSingleEvent("ES4", 1, new string('.', 3000), eventId: Guid.NewGuid(), eventType: "other-event-type-4",
				retryOnFail: true);

			_forwardReadPos = new TFPos(firstEvent.LogPosition, firstEvent.LogPosition);
			_backwardReadPos = new TFPos(WriterCheckpoint.ReadNonFlushed(), WriterCheckpoint.ReadNonFlushed());
		}

		[Test]
		public void should_read_only_events_forward_with_event_type_prefix() {
			var eventFilter = EventFilter.EventType.Prefixes("event-type");

			var result = ReadIndex.ReadAllEventsForwardFiltered(_forwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(2, result.Records.Count);
		}

		[Test]
		public void should_read_only_events_forward_with_event_type_regex() {
			var eventFilter = EventFilter.EventType.Regex(@"^.*other-event.*$");

			var result = ReadIndex.ReadAllEventsForwardFiltered(_forwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(2, result.Records.Count);
		}

		[Test]
		public void should_read_only_events_forward_with_stream_id_prefix() {
			var eventFilter = EventFilter.StreamName.Prefixes("ES2");

			var result = ReadIndex.ReadAllEventsForwardFiltered(_forwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(1, result.Records.Count);
		}

		[Test]
		public void should_read_only_events_forward_with_stream_id_regex() {
			var eventFilter = EventFilter.StreamName.Regex(@"^.*ES2.*$");

			var result = ReadIndex.ReadAllEventsForwardFiltered(_forwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(1, result.Records.Count);
		}

		[Test]
		public void should_read_only_events_backward_with_event_type_prefix() {
			var eventFilter = EventFilter.EventType.Prefixes("event-type");

			var result = ReadIndex.ReadAllEventsBackwardFiltered(_backwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(2, result.Records.Count);
		}

		[Test]
		public void should_read_only_events_backward_with_event_type_regex() {
			var eventFilter = EventFilter.EventType.Regex(@"^.*other-event.*$");

			var result = ReadIndex.ReadAllEventsBackwardFiltered(_backwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(2, result.Records.Count);
		}

		[Test]
		public void should_read_only_events_backward_with_stream_id_prefix() {
			var eventFilter = EventFilter.StreamName.Prefixes("ES2");

			var result = ReadIndex.ReadAllEventsBackwardFiltered(_backwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(1, result.Records.Count);
		}

		[Test]
		public void should_read_only_events_backward_with_stream_id_regex() {
			var eventFilter = EventFilter.StreamName.Regex(@"^.*ES2.*$");

			var result = ReadIndex.ReadAllEventsBackwardFiltered(_backwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(1, result.Records.Count);
		}
	}
}
