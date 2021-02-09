using System;
using System.Text;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;
using EventStore.Core.Tests.Services.TimeService;

namespace EventStore.Projections.Core.Tests.Services.projection_subscription {
	[TestFixture("$all", "specific-event", "test-stream", "bad-event-type", true)]
	[TestFixture("$all", "specific-event", "test-stream", "bad-event-type", false)]
	public class
		when_handling_events_that_exceed_unhandled_bytes_threshold_and_checkpoint_after_reached :
			TestFixtureWithProjectionSubscription {
		private string _sourceStream;
		private string _sourceEventType;
		private string _stream;
		private string _eventType;
		private bool _exceedUnhandledBytesThreshold;

		public when_handling_events_that_exceed_unhandled_bytes_threshold_and_checkpoint_after_reached(
			string sourceStream, string sourceEventType, string stream, string eventType, bool exceedUnhandledBytesThreshold) {
			_sourceStream = sourceStream;
			_sourceEventType = sourceEventType;
			_stream = stream;
			_eventType = eventType;
			_exceedUnhandledBytesThreshold = exceedUnhandledBytesThreshold;
		}

		protected override void Given() {
			_source = source => {
				if (_sourceStream == "$all") {
					source.FromAll();
				} else {
					source.FromStream(_sourceStream);
				}
				source.IncludeEvent(_sourceEventType);
			};
			_checkpointAfterMs = 1000;
			_checkpointProcessedEventsThreshold = 1;
			_checkpointUnhandledBytesThreshold = 4096;
			_timeProvider = new FakeTimeProvider();
		}

		protected override void When() {
			var firstPosition = new TFPos(200, 200);
			var offset = _exceedUnhandledBytesThreshold
				? (_checkpointUnhandledBytesThreshold + 1)
				: (_checkpointUnhandledBytesThreshold - 1);
			var secondPosition = new TFPos(firstPosition.CommitPosition + offset, firstPosition.PreparePosition + offset);
			
			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), firstPosition, "test-stream", 1, false, Guid.NewGuid(),
					"bad-event-type", false, new byte[0], new byte[0]));
			_checkpointHandler.HandledMessages.Clear();
			((FakeTimeProvider)_timeProvider).AddToUtcTime(TimeSpan.FromMilliseconds(_checkpointAfterMs + 1));
			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), secondPosition, "test-stream", 1, false, Guid.NewGuid(),
					"bad-event-type", false, new byte[0], new byte[0]));
		}

		[Test]
		public void checkpoint_is_suggested_depending_on_whether_unhandled_bytes_threshold_was_exceeded() {
			Assert.AreEqual(_exceedUnhandledBytesThreshold ? 1 : 0, _checkpointHandler.HandledMessages.Count);
		}
	}
}
