using System.Collections.Generic;
using System;
using EventStore.ClientAPI;
using NUnit.Framework;
using EventStore.Core.Data;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.Tests.ClientAPI.ExpectedVersion64Bit {
	[TestFixture]
	[Category("ClientAPI"), Category("LongRunning")]
	public class subscriptions_on_stream_with_event_numbers_greater_than_2_billion : MiniNodeWithExistingRecords {
		private const long intMaxValue = (long)int.MaxValue;

		private string _volatileStreamOne = "subscriptions-volatile-1";
		private string _volatileStreamTwo = "subscriptions-volatile-2";
		private string _catchupStreamOne = "subscriptions-catchup-1";

		private EventRecord _c1, _c2;

		public override void WriteTestScenario() {
			WriteSingleEvent(_volatileStreamOne, intMaxValue + 1, new string('.', 3000));
			WriteSingleEvent(_volatileStreamOne, intMaxValue + 2, new string('.', 3000));

			WriteSingleEvent(_volatileStreamTwo, intMaxValue + 1, new string('.', 3000));
			WriteSingleEvent(_volatileStreamTwo, intMaxValue + 2, new string('.', 3000));

			_c1 = WriteSingleEvent(_catchupStreamOne, intMaxValue + 1, new string('.', 3000));
			_c2 = WriteSingleEvent(_catchupStreamOne, intMaxValue + 2, new string('.', 3000));
		}

		public override void Given() {
			_store = BuildConnection(Node);
			_store.ConnectAsync().Wait();
			_store.SetStreamMetadataAsync(_volatileStreamOne, EventStore.ClientAPI.ExpectedVersion.Any,
				EventStore.ClientAPI.StreamMetadata.Create(truncateBefore: intMaxValue + 1)).Wait();
			_store.SetStreamMetadataAsync(_volatileStreamTwo, EventStore.ClientAPI.ExpectedVersion.Any,
				EventStore.ClientAPI.StreamMetadata.Create(truncateBefore: intMaxValue + 1)).Wait();
			_store.SetStreamMetadataAsync(_catchupStreamOne, EventStore.ClientAPI.ExpectedVersion.Any,
				EventStore.ClientAPI.StreamMetadata.Create(truncateBefore: intMaxValue + 1)).Wait();
		}

		[Test]
		public void should_be_able_to_subscribe_to_stream_with_volatile_subscription() {
			var evnt = new EventData(Guid.NewGuid(), "EventType", false, new byte[10], new byte[15]);
			EventStore.ClientAPI.ResolvedEvent receivedEvent = new EventStore.ClientAPI.ResolvedEvent();
			var mre = new ManualResetEvent(false);
			_store.SubscribeToStreamAsync(_volatileStreamOne, true, (s, e) => {
				receivedEvent = e;
				mre.Set();
				return Task.CompletedTask;
			}).Wait();

			_store.AppendToStreamAsync(_volatileStreamOne, intMaxValue + 2, evnt).Wait();
			Assert.That(mre.WaitOne(TimeSpan.FromSeconds(5)), "Timed out waiting for events to appear");

			Assert.AreEqual(evnt.EventId, receivedEvent.Event.EventId);
		}

		[Test]
		public void should_be_able_to_subscribe_to_all_with_volatile_subscription() {
			var evnt = new EventData(Guid.NewGuid(), "EventType", false, new byte[10], new byte[15]);
			EventStore.ClientAPI.ResolvedEvent receivedEvent = new EventStore.ClientAPI.ResolvedEvent();
			var mre = new ManualResetEvent(false);
			_store.SubscribeToAllAsync(true, (s, e) => {
				receivedEvent = e;
				mre.Set();
				return Task.CompletedTask;
			}, userCredentials: DefaultData.AdminCredentials).Wait();

			_store.AppendToStreamAsync(_volatileStreamTwo, intMaxValue + 2, evnt).Wait();
			Assert.That(mre.WaitOne(TimeSpan.FromSeconds(5)), "Timed out waiting for events to appear");

			Assert.AreEqual(evnt.EventId, receivedEvent.Event.EventId);
		}

		[Test]
		public void should_be_able_to_subscribe_to_stream_with_catchup_subscription() {
			var evnt = new EventData(Guid.NewGuid(), "EventType", false, new byte[10], new byte[15]);
			List<EventStore.ClientAPI.ResolvedEvent> receivedEvents = new List<EventStore.ClientAPI.ResolvedEvent>();

			var countdown = new CountdownEvent(3);
			_store.SubscribeToStreamFrom(_catchupStreamOne, 0, CatchUpSubscriptionSettings.Default, (s, e) => {
				receivedEvents.Add(e);
				countdown.Signal();
				return Task.CompletedTask;
			});

			_store.AppendToStreamAsync(_catchupStreamOne, intMaxValue + 2, evnt).Wait();

			Assert.That(countdown.Wait(TimeSpan.FromSeconds(5)), "Timed out waiting for events to appear");

			Assert.AreEqual(3, receivedEvents.Count);
			Assert.AreEqual(_c1.EventId, receivedEvents[0].Event.EventId);
			Assert.AreEqual(_c2.EventId, receivedEvents[1].Event.EventId);
			Assert.AreEqual(evnt.EventId, receivedEvents[2].Event.EventId);
		}
	}
}
