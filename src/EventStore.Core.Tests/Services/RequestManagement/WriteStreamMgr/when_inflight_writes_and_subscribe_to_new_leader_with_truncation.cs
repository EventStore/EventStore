using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using EventStore.Core.Services.RequestManager.Managers;

namespace EventStore.Core.Tests.Services.RequestManagement.WriteStreamMgr {
	[TestFixture]
	public class when_inflight_writes_and_subscribe_to_new_leader_with_truncation : RequestManagerSpecification<WriteEvents> {
		const long EventPosition = 100;
		const long SubscribedPosition = EventPosition; // the written event will be truncated
		const long LeaderReplicatedToPosition = 105;
		protected override WriteEvents OnManager(FakePublisher publisher) {
			return new WriteEvents(
				publisher, 
				CommitTimeout, 
				Envelope,
				InternalCorrId,
				ClientCorrId,
				"test123",
				ExpectedVersion.Any,
				new[] {DummyEvent()},
				CommitSource);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new StorageMessage.PrepareAck(InternalCorrId, EventPosition, PrepareFlags.SingleWrite|PrepareFlags.Data);
			yield return new ReplicationMessage.ReplicaSubscribed(Guid.NewGuid(), Guid.NewGuid(), SubscribedPosition);
			yield return new ReplicationTrackingMessage.ReplicatedTo(LeaderReplicatedToPosition);
			yield return new ReplicationTrackingMessage.IndexedTo(EventPosition);
		}

		protected override Message When() => new StorageMessage.CommitIndexed(InternalCorrId, EventPosition, EventPosition, 0, 0);

		[Test]
		public void no_additional_messages_are_published() => Assert.That(Produced.Count == 0);

		[Test]
		public void the_envelope_has_no_additional_replies() => Assert.AreEqual(0, Envelope.Replies.Count);
	}
}
