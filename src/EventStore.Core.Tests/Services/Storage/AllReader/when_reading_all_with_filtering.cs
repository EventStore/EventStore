using System;
using NUnit.Framework;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Util;
using static EventStore.Core.Messages.TcpClientMessageDto.Filter;

namespace EventStore.Core.Tests.Services.Storage.AllReader {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_reading_all_with_filtering<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
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
			var filter = new TcpClientMessageDto.Filter(
				FilterContext.EventType,
				FilterType.Prefix, new[] {"event-type"});
			var eventFilter = EventFilter.Get(true, filter);

			var result = ReadIndex.ReadAllEventsForwardFiltered(_forwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(2, result.Records.Count);
		}

		[Test]
		public void should_read_only_events_forward_with_event_type_regex() {
			var filter = new TcpClientMessageDto.Filter(
				FilterContext.EventType,
				FilterType.Regex, new[] {@"^.*other-event.*$"});
			var eventFilter = EventFilter.Get(true, filter);

			var result = ReadIndex.ReadAllEventsForwardFiltered(_forwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(2, result.Records.Count);
		}

		[Test]
		public void should_read_only_events_forward_with_stream_id_prefix() {
			var filter = new TcpClientMessageDto.Filter(
				FilterContext.StreamId,
				FilterType.Prefix, new[] {"ES2"});
			var eventFilter = EventFilter.Get(true, filter);

			var result = ReadIndex.ReadAllEventsForwardFiltered(_forwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(1, result.Records.Count);
		}

		[Test]
		public void should_read_only_events_forward_with_stream_id_regex() {
			var filter = new TcpClientMessageDto.Filter(
				FilterContext.StreamId,
				FilterType.Regex, new[] {@"^.*ES2.*$"});
			var eventFilter = EventFilter.Get(true, filter);

			var result = ReadIndex.ReadAllEventsForwardFiltered(_forwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(1, result.Records.Count);
		}

		[Test]
		public void should_read_only_events_backward_with_event_type_prefix() {
			var filter = new TcpClientMessageDto.Filter(
				FilterContext.EventType,
				FilterType.Prefix, new[] {"event-type"});
			var eventFilter = EventFilter.Get(true, filter);

			var result = ReadIndex.ReadAllEventsBackwardFiltered(_backwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(2, result.Records.Count);
		}

		[Test]
		public void should_read_only_events_backward_with_event_type_regex() {
			var filter = new TcpClientMessageDto.Filter(
				FilterContext.EventType,
				FilterType.Regex, new[] {@"^.*other-event.*$"});
			var eventFilter = EventFilter.Get(true, filter);

			var result = ReadIndex.ReadAllEventsBackwardFiltered(_backwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(2, result.Records.Count);
		}

		[Test]
		public void should_read_only_events_backward_with_stream_id_prefix() {
			var filter = new TcpClientMessageDto.Filter(
				FilterContext.StreamId,
				FilterType.Prefix, new[] {"ES2"});
			var eventFilter = EventFilter.Get(true, filter);

			var result = ReadIndex.ReadAllEventsBackwardFiltered(_backwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(1, result.Records.Count);
		}

		[Test]
		public void should_read_only_events_backward_with_stream_id_regex() {
			var filter = new TcpClientMessageDto.Filter(
				FilterContext.StreamId,
				FilterType.Regex, new[] {@"^.*ES2.*$"});
			var eventFilter = EventFilter.Get(true, filter);

			var result = ReadIndex.ReadAllEventsBackwardFiltered(_backwardReadPos, 10, 10, eventFilter);
			Assert.AreEqual(1, result.Records.Count);
		}
	}
}
