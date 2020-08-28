using System.Collections.Generic;
using System;
using EventStore.ClientAPI;
using NUnit.Framework;
using EventStore.Core.Data;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Data;

namespace EventStore.Core.Tests.ClientAPI.ExpectedVersion64Bit {
	[TestFixture]
	[Category("ClientAPI"), Category("LongRunning")]
	public class catchup_subscription_to_all_with_event_numbers_greater_than_2_billion : MiniNodeWithExistingRecords {
		private const long intMaxValue = (long)int.MaxValue;

		private string _streamId = "subscriptions-catchup-all";

		private EventRecord _r1, _r2;

		public override void WriteTestScenario() {
			_r1 = WriteSingleEvent(_streamId, intMaxValue + 1, new string('.', 3000));
			_r2 = WriteSingleEvent(_streamId, intMaxValue + 2, new string('.', 3000));
		}

		public override async Task Given() {
			_store = BuildConnection(Node);
			await _store.ConnectAsync();
			await _store.SetStreamMetadataAsync(_streamId, EventStore.ClientAPI.ExpectedVersion.Any,
				EventStore.ClientAPI.StreamMetadata.Create(truncateBefore: intMaxValue + 1));
		}

		[Test]
		public async Task should_be_able_to_subscribe_to_all_with_catchup_subscription() {
			var evnt = new EventData(Guid.NewGuid(), "EventType", false, new byte[10], new byte[15]);
			List<EventStore.ClientAPI.ResolvedEvent> receivedEvents = new List<EventStore.ClientAPI.ResolvedEvent>();

			var countdown = new CountdownEvent(3);

			_store.SubscribeToAllFrom(Position.Start, CatchUpSubscriptionSettings.Default, (s, e) => {
				if (e.Event.EventStreamId == _streamId) {
					receivedEvents.Add(e);
					countdown.Signal();
				}

				return Task.CompletedTask;
			}, userCredentials: DefaultData.AdminCredentials);

			await _store.AppendToStreamAsync(_streamId, intMaxValue + 2, evnt);

			Assert.That(countdown.Wait(TimeSpan.FromSeconds(10)), "Timed out waiting for events to appear");

			Assert.AreEqual(_r1.EventId, receivedEvents[0].Event.EventId);
			Assert.AreEqual(_r2.EventId, receivedEvents[1].Event.EventId);
			Assert.AreEqual(evnt.EventId, receivedEvents[2].Event.EventId);
		}
	}
}
