using System;
using System.Collections.Generic;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using TransactionCommitMgr = EventStore.Core.Services.RequestManager.Managers.TransactionCommit;

namespace EventStore.Core.Tests.Services.Replication.Transaction {
	[TestFixture]
	public class when_transaction_commit_gets_prepare_timeout_before_prepares : RequestManagerSpecification<TransactionCommitMgr> {
		
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
				this);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield break;
		}

		protected override Message When() {
			return new StorageMessage.RequestManagerTimerTick(
				DateTime.UtcNow + PrepareTimeout + TimeSpan.FromMinutes(5));
		}

		[Test]
		public void failed_request_message_is_published() {
			Assert.That(Produced.ContainsSingle<StorageMessage.RequestCompleted>(
				x => x.CorrelationId == InternalCorrId && x.Success == false));
		}

		[Test]
		public void the_envelope_is_replied_to_with_failure() {
			Assert.That(Envelope.Replies.ContainsSingle<ClientMessage.TransactionCommitCompleted>(
				x => x.CorrelationId == ClientCorrId && x.Result == OperationResult.PrepareTimeout));
		}
	}
}
