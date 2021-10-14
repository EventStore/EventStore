using System;
using System.Collections.Generic;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Bus.Helpers;

namespace EventStore.Core.Tests.Services.RequestManagement.TransactionMgr {
	[TestFixture]
	public class when_transaction_commit_gets_prepare_timeout_before_prepares : RequestManagerSpecification<TransactionCommit> {
		
		private int transactionId = 2341;
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
			yield break;
		}

		protected override Message When() {
			Manager.RequestTimedOut();
			return new TestMessage();
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
