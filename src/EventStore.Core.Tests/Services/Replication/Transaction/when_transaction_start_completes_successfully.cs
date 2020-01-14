using System;
using System.Collections.Generic;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;


namespace EventStore.Core.Tests.Services.Replication.Transaction {
	[TestFixture]
	public class when_transaction_start_completes_successfully : RequestManagerSpecification<TransactionStart> {
		private long _commitPosition =3000;
		private long _transactionPosition = 1564;
		protected override TransactionStart OnManager(FakePublisher publisher) {
			return new TransactionStart(
			 	publisher,
				PrepareTimeout,
				Envelope,
				InternalCorrId,
				ClientCorrId,
				$"testStream-{nameof(when_transaction_commit_completes_successfully)}",
				true,				
				0,
			    null,
				this);
			}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new StorageMessage.PrepareAck(InternalCorrId, _transactionPosition, PrepareFlags.TransactionBegin);
		}

		protected override Message When() {
			LogCommitPosition = _commitPosition;
			return new StorageMessage.RequestManagerTimerTick(DateTime.UtcNow);
		}

		[Test]
		public void successful_request_message_is_published() {
			Assert.That(Produced.ContainsSingle<StorageMessage.RequestCompleted>(
				x => x.CorrelationId == InternalCorrId && x.Success));
		}

		[Test]
		public void the_envelope_is_replied_to_with_success() {
			Assert.That(Envelope.Replies.ContainsSingle<ClientMessage.TransactionStartCompleted>(
				x => 
					x.CorrelationId == ClientCorrId && 
					x.Result == OperationResult.Success &&
					x.TransactionId == _transactionPosition));
		}
	}
}
