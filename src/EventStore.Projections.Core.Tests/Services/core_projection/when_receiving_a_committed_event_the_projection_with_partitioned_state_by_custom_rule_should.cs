using System;
using System.Linq;
using System.Text;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.Data;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.core_projection {
	[TestFixture]
	public class when_receiving_a_committed_event_the_projection_with_partitioned_state_by_custom_rule_should :
		TestFixtureWithCoreProjectionStarted {
		private Guid _eventId;

		protected override void Given() {
			_configureBuilderByQuerySource = source => {
				source.FromAll();
				source.AllEvents();
				source.SetByCustomPartitions();
				source.SetDefinesStateTransform();
			};
			TicksAreHandledImmediately();
			AllWritesSucceed();
			NoOtherStreams();
		}

		protected override FakeProjectionStateHandler GivenProjectionStateHandler() {
			return new FakeProjectionStateHandler(
				configureBuilder: _configureBuilderByQuerySource, failOnGetPartition: false);
		}

		protected override void When() {
			//projection subscribes here
			_eventId = Guid.NewGuid();
			_consumer.HandledMessages.Clear();
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"account-01", -1, "account-01", -1, false, new TFPos(120, 110), _eventId,
						"handle_this_type", false, "data", "metadata"), _subscriptionId, 0));
		}

		[Test]
		public void request_partition_state_from_the_correct_stream() {
			Assert.AreEqual(
				1,
				_consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsBackward>()
					.Count(v => v.EventStreamId == "$projections-projection-region-a-checkpoint"));
		}

		[Test]
		public void update_state_snapshot_is_written_to_the_correct_stream() {
			Assert.AreEqual(1, _writeEventHandler.HandledMessages.OfEventType("Result").Count);
			var message = _writeEventHandler.HandledMessages.WithEventType("Result")[0];
			Assert.AreEqual("$projections-projection-region-a-result", message.EventStreamId);
		}

		[Test]
		public void pass_partition_name_to_state_handler() {
			Assert.AreEqual("region-a", _stateHandler._lastPartition);
		}
	}
}
