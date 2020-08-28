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
	public class persistent_subscription_with_event_numbers_greater_than_2_billion : MiniNodeWithExistingRecords {
		private const long intMaxValue = (long)int.MaxValue;

		private string _streamId = "persistent-subscription-stream";

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
		public async Task should_be_able_to_create_the_persistent_subscription() {
			var groupId = "group-" + Guid.NewGuid().ToString();
			var settings = PersistentSubscriptionSettings.Create().StartFrom(intMaxValue);
			await _store.CreatePersistentSubscriptionAsync(_streamId, groupId, settings, DefaultData.AdminCredentials);
		}

		[Test]
		public async Task should_be_able_to_update_the_persistent_subscription() {
			var groupId = "group-" + Guid.NewGuid().ToString();
			var settings = PersistentSubscriptionSettings.Create();
			await _store.CreatePersistentSubscriptionAsync(_streamId, groupId, settings, DefaultData.AdminCredentials);

			settings = PersistentSubscriptionSettings.Create().StartFrom(intMaxValue);
			await _store.UpdatePersistentSubscriptionAsync(_streamId, groupId, settings, DefaultData.AdminCredentials);
		}

		[Test]
		public async Task should_be_able_to_connect_to_persistent_subscription() {
			var groupId = "group-" + Guid.NewGuid().ToString();
			var settings = PersistentSubscriptionSettings.Create().StartFrom(intMaxValue);
			await _store.CreatePersistentSubscriptionAsync(_streamId, groupId, settings, DefaultData.AdminCredentials);

			var evnt = new EventData(Guid.NewGuid(), "EventType", false, new byte[10], new byte[15]);
			List<EventStore.ClientAPI.ResolvedEvent> receivedEvents = new List<EventStore.ClientAPI.ResolvedEvent>();
			var countdown = new CountdownEvent(3);
			await _store.ConnectToPersistentSubscriptionAsync(_streamId, groupId, (s, e) => {
				receivedEvents.Add(e);
				countdown.Signal();
				return Task.CompletedTask;
			}, userCredentials: DefaultData.AdminCredentials);

			await _store.AppendToStreamAsync(_streamId, intMaxValue + 2, evnt);

			Assert.That(countdown.Wait(TimeSpan.FromSeconds(5)), "Timed out waiting for events to appear");

			Assert.AreEqual(_r1.EventId, receivedEvents[0].Event.EventId);
			Assert.AreEqual(_r2.EventId, receivedEvents[1].Event.EventId);
			Assert.AreEqual(evnt.EventId, receivedEvents[2].Event.EventId);
		}
	}
}
