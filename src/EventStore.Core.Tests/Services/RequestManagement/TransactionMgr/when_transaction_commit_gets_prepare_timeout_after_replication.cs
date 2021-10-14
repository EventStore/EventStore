using System;
using System.Collections.Generic;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.Tests.Helpers;
using System.Threading;

namespace EventStore.Core.Tests.Services.RequestManagement.TransactionMgr {
	[TestFixture]
	public class when_transaction_commit_gets_prepare_timeout_after_replication : RequestManagerSpecification<TransactionCommit> {

		private int transactionId = 2341;
		private int preparePosition = 100;
		protected override TransactionCommit OnManager(FakePublisher publisher) {
			return new TransactionCommit(
				publisher,
				1,
				CommitTimeout,
				Envelope,
				InternalCorrId,
				ClientCorrId,
				transactionId,
				CommitSource);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new StorageMessage.PrepareAck(InternalCorrId, preparePosition, PrepareFlags.SingleWrite);
			yield return new StorageMessage.CommitIndexed(InternalCorrId, preparePosition, 1, 0, 0);
			yield return new ReplicationTrackingMessage.ReplicatedTo(preparePosition);
		}

		protected override Message When() {
			Thread.Yield();
			Manager.RequestTimedOut();
			return new TestMessage();
		}

		[Test]
		public void no_additional_messages_are_published() {
			Assert.That(Produced.Count == 0);
		}
		[Test]
		public void the_envelope_has_no_additional_replies() {
			Assert.AreEqual(0, Envelope.Replies.Count);
		}
	}
}
