using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.TransactionCommit {
	[TestFixture]
	public class when_transaction_commit_gets_already_committed_and_log_is_not_committed : RequestManagerSpecification {
		private long _commitPosition = 1000;
		protected override IRequestManager OnManager(FakePublisher publisher) {
			return new TransactionCommitTwoPhaseRequestManager(publisher, 3, PrepareTimeout, CommitTimeout, false);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new ClientMessage.TransactionCommit(InternalCorrId, ClientCorrId, Envelope, true, 4, null);
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
