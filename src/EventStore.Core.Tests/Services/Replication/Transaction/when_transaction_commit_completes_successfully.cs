using System;
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using TransactionCommitMgr = EventStore.Core.Services.RequestManager.Managers.TransactionCommit;

namespace EventStore.Core.Tests.Services.Replication.Transaction {
	[TestFixture]
	public class when_transaction_commit_completes_successfully : RequestManagerSpecification<TransactionCommitMgr> {
		private long _commitPosition =3000;
		private long _transactionPostion = 1000;
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
			yield return new StorageMessage.PrepareAck(InternalCorrId, _transactionPostion, PrepareFlags.TransactionEnd);
			yield return new StorageMessage.CommitAck(InternalCorrId, _commitPosition, _transactionPostion, 1, 3);
			yield return new CommitMessage.LogCommittedTo(_commitPosition);
		}

		protected override Message When() {
			return new StorageMessage.CommitIndexed(InternalCorrId,_commitPosition,_transactionPostion,0,0);
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
