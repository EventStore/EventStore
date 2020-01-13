using System;
using System.Collections.Generic;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services.Replication;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using TransactionCommitMgr = EventStore.Core.Services.RequestManager.Managers.TransactionCommit;

namespace EventStore.Core.Tests.Services.RequestManagement.TransactionMgr {
	[TestFixture]
	public class when_transaction_commit_gets_prepare_timeout_after_prepares : RequestManagerSpecification<TransactionCommitMgr> {

		private int transactionId = 2341;
		protected override TransactionCommitMgr OnManager(FakePublisher publisher) {
			return new TransactionCommitMgr(
				publisher,
				PrepareTimeout,
				CommitTimeout,
				Envelope,
				InternalCorrId,
				ClientCorrId,
				transactionId,
				true,
				null,
				CommitSource);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new StorageMessage.PrepareAck(InternalCorrId, transactionId, PrepareFlags.SingleWrite);
		}

		protected override Message When() {
			return new StorageMessage.RequestManagerTimerTick(
				DateTime.UtcNow + TimeSpan.FromTicks(CommitTimeout.Ticks / 2));
		}

		[Test]
		public void no_messages_are_published() {
			Assert.That(Produced.Count == 0);
		}

		[Test]
		public void the_envelope_is_not_replied_to() {
			Assert.AreEqual(0, Envelope.Replies.Count);
		}
	}
}
