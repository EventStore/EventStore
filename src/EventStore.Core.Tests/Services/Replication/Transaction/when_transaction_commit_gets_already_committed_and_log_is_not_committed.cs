using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using NUnit.Framework;
using TransactionCommitMgr = EventStore.Core.Services.RequestManager.Managers.TransactionCommit;

namespace EventStore.Core.Tests.Services.Replication.Transaction {
	[TestFixture]
	public class when_transaction_commit_gets_already_committed_and_log_is_not_committed : RequestManagerSpecification<TransactionCommitMgr> {
		private long _commitPosition = 1000;
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
			yield return new CommitMessage.CommittedTo(_commitPosition - 1);
		}

		protected override Message When() {
			return new StorageMessage.AlreadyCommitted(InternalCorrId, "test123", 0, 1, _commitPosition);
		}

		[Test]
		public void messages_are_not_publised() {
			Assert.That(!Produced.Any());
		}

		[Test]
		public void the_envelope_is_not_replied_to() {
			Assert.That(!Envelope.Replies.Any());
		}
	}
}
