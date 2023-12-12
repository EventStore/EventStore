extern alias GrpcClient;
extern alias GrpcClientPersistent;
extern alias GrpcClientStreams;
using System.Collections.Generic;
using System;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Data;
using GrpcClientPersistent::EventStore.Client;
using EventData = GrpcClient::EventStore.Client.EventData;
using ResolvedEvent = GrpcClient::EventStore.Client.ResolvedEvent;
using StreamMetadata = GrpcClientStreams::EventStore.Client.StreamMetadata;
using StreamPosition = GrpcClient::EventStore.Client.StreamPosition;
using Uuid = GrpcClient::EventStore.Client.Uuid;

namespace EventStore.Core.Tests.ClientAPI.ExpectedVersion64Bit {
	extern alias GrpcClientStreams;

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	[Category("ClientAPI"), Category("LongRunning")]
	public class persistent_subscription_with_event_numbers_greater_than_2_billion<TLogFormat, TStreamId>
		: MiniNodeWithExistingRecords<TLogFormat, TStreamId> {
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
			await _store.SetStreamMetadataAsync(_streamId, ExpectedVersion.Any,
				new StreamMetadata(truncateBefore: intMaxValue + 1));
		}

		[Test]
		public async Task should_be_able_to_create_the_persistent_subscription() {
			var groupId = "group-" + Guid.NewGuid().ToString();
			var settings = new PersistentSubscriptionSettings(startFrom: StreamPosition.FromInt64(intMaxValue));
			await _store.CreatePersistentSubscriptionAsync(_streamId, groupId, settings, DefaultData.AdminCredentials);
		}

		[Test]
		public async Task should_be_able_to_update_the_persistent_subscription() {
			var groupId = "group-" + Guid.NewGuid().ToString();
			var settings = new PersistentSubscriptionSettings();
			await _store.CreatePersistentSubscriptionAsync(_streamId, groupId, settings, DefaultData.AdminCredentials);

			settings = new PersistentSubscriptionSettings(startFrom: StreamPosition.FromInt64(intMaxValue));
			await _store.UpdatePersistentSubscriptionAsync(_streamId, groupId, settings, DefaultData.AdminCredentials);
		}

		[Test]
		public async Task should_be_able_to_connect_to_persistent_subscription() {
			var groupId = "group-" + Guid.NewGuid().ToString();
			var settings = new PersistentSubscriptionSettings(startFrom: StreamPosition.FromInt64(intMaxValue));
			await _store.CreatePersistentSubscriptionAsync(_streamId, groupId, settings, DefaultData.AdminCredentials);

			var evnt = new EventData(Uuid.NewUuid(), "EventType", new byte[10], new byte[15]);
			List<ResolvedEvent> receivedEvents = new List<ResolvedEvent>();
			var countdown = new CountdownEvent(3);
			await _store.ConnectToPersistentSubscriptionAsync(_streamId, groupId, (s, e) => {
				receivedEvents.Add(e);
				countdown.Signal();
				return Task.CompletedTask;
			}, userCredentials: DefaultData.AdminCredentials);

			await _store.AppendToStreamAsync(_streamId, intMaxValue + 2, evnt);

			Assert.That(countdown.Wait(TimeSpan.FromSeconds(5)), "Timed out waiting for events to appear");

			Assert.AreEqual(_r1.EventId, receivedEvents[0].Event.EventId.ToGuid());
			Assert.AreEqual(_r2.EventId, receivedEvents[1].Event.EventId.ToGuid());
			Assert.AreEqual(evnt.EventId, receivedEvents[2].Event.EventId);
		}
	}
}
