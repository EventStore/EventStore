using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using WriteEventsMgr =EventStore.Core.Services.RequestManager.Managers.WriteEvents;

namespace EventStore.Core.Tests.Services.Replication.WriteStream {
	[TestFixture]
	public class when_write_stream_gets_timeout_after_cluster_commit : RequestManagerSpecification<WriteEventsMgr> {
		private long _prepareLogPosition = 100;
		private long _commitPosition = 100;
		protected override WriteEventsMgr OnManager(FakePublisher publisher) {
			return new WriteEventsMgr(
				publisher, 
				CommitTimeout, 
				Envelope,
				InternalCorrId,
				ClientCorrId,
				"test123",
				true,
				ExpectedVersion.Any,
				null,
				new[] {DummyEvent()},
				this);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new StorageMessage.PrepareAck(InternalCorrId, _prepareLogPosition, PrepareFlags.SingleWrite|PrepareFlags.Data);
			yield return new StorageMessage.CommitAck(InternalCorrId, _commitPosition, 1, 0, 0);
		}

		protected override Message When() {
			CommitPosition = _commitPosition;
			return new StorageMessage.RequestManagerTimerTick(DateTime.UtcNow + CommitTimeout + CommitTimeout);
		}

		[Test]
		public void no_additional_messages_are_published() {
			Assert.That(Produced.Count == 0);
		}
		[Test]
		public void the_envelope_has_single_successful_reply() {
			Assert.AreEqual(1, Envelope.Replies.Count);
			Assert.AreEqual(OperationResult.Success, ((ClientMessage.WriteEventsCompleted)Envelope.Replies[0]).Result);
		}
	}
}
