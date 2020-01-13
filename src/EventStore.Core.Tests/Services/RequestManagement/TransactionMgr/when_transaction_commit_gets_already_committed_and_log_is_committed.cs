using System;
using System.Collections.Generic;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.Services.Replication;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using TransactionCommitMgr = EventStore.Core.Services.RequestManager.Managers.TransactionCommit;

namespace EventStore.Core.Tests.Services.RequestManagement.TransactionMgr {
	[TestFixture]
	public class when_transaction_commit_gets_already_committed_and_log_is_committed : RequestManagerSpecification<TransactionCommitMgr> {

		private int transactionId = 2341;
		private long commitPosition = 3000;
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
			yield return new StorageMessage.PrepareAck(InternalCorrId, transactionId, PrepareFlags.TransactionEnd);
			yield return new StorageMessage.CommitAck(Guid.NewGuid(), commitPosition, transactionId, 0, 10);
		}

		protected override Message When() {
			return new StorageMessage.AlreadyCommitted(InternalCorrId, "test123", 0, 1, commitPosition);
		}

		[Test]
		public void successful_request_message_is_publised() {
			Assert.That(Produced.ContainsSingle<StorageMessage.RequestCompleted>(
				x => x.CorrelationId == InternalCorrId && x.Success));
		}

		[Test]
		public void the_envelope_is_replied_to_with_success() {
			Assert.That(Envelope.Replies.ContainsSingle<ClientMessage.TransactionCommitCompleted>(
				x => x.CorrelationId == ClientCorrId && x.Result == OperationResult.Success));
		}
	}
}
