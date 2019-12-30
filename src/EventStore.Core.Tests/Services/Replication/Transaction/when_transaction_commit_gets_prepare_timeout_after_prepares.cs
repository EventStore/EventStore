using System;
using System.Collections.Generic;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using TransactionCommitMgr = EventStore.Core.Services.RequestManager.Managers.TransactionCommit;

namespace EventStore.Core.Tests.Services.Replication.Transaction {
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
				null);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new StorageMessage.PrepareAck(InternalCorrId, transactionId, PrepareFlags.SingleWrite);
		}

		protected override Message When() {
			return new StorageMessage.RequestManagerTimerTick(
				DateTime.UtcNow + TimeSpan.FromTicks(CommitTimeout.Ticks / 2));
		}

		[Test]
		public void fail_message_are_published() {
			Assert.That(Produced.Count == 1);
			var response = Produced[0] as StorageMessage.RequestCompleted;
			Assert.NotNull(response);
			Assert.AreEqual(false, response.Success);
			Assert.AreEqual(InternalCorrId, response.CorrelationId);
		}

		[Test]
		public void the_envelope_is_replied_fail() {
			Assert.AreEqual(1, Envelope.Replies.Count);
			var response = Envelope.Replies[0] as ClientMessage.TransactionCommitCompleted;
			Assert.NotNull(response);
			Assert.AreEqual(OperationResult.CommitTimeout, response.Result);
			Assert.AreEqual(transactionId, response.TransactionId);
		}
	}
}
