using System.Collections.Generic;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.TransactionCommit {
	[TestFixture]
	public class when_transaction_commit_completes_successfully : RequestManagerSpecification {
		private long _commitPosition =3000;
		private long _transactionPostion = 1000;
		private int transactionId = 2341;
		protected override IRequestManager OnManager(FakePublisher publisher) {
			return new TransactionCommitTwoPhaseRequestManager(publisher, 3, PrepareTimeout, CommitTimeout, false);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new ClientMessage.TransactionCommit(InternalCorrId, ClientCorrId, Envelope, true, transactionId, null);
			yield return new StorageMessage.PrepareAck(InternalCorrId, _transactionPostion, PrepareFlags.Data);
			yield return new StorageMessage.PrepareAck(InternalCorrId, _transactionPostion + 100, PrepareFlags.Data);
			yield return new StorageMessage.PrepareAck(InternalCorrId, _transactionPostion + 200, PrepareFlags.Data);
			yield return new StorageMessage.CommitAck(InternalCorrId, _commitPosition, _transactionPostion, 1, 3);
		}

		protected override Message When() {
			return new CommitMessage.CommittedTo(_commitPosition);
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
