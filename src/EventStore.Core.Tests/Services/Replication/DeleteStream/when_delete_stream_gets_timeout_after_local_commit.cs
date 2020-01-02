using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using DeleteStreamManager = EventStore.Core.Services.RequestManager.Managers.DeleteStream;

namespace EventStore.Core.Tests.Services.Replication.DeleteStream {
	public class when_delete_stream_gets_timeout_after_local_commit : RequestManagerSpecification<DeleteStreamManager> {
		private long _commitPosition = 1000;
		protected override DeleteStreamManager OnManager(FakePublisher publisher) {
			return new DeleteStreamManager(
				publisher, 
				CommitTimeout, 
				Envelope,
				InternalCorrId,
				ClientCorrId,
				"test123",
				true,
				ExpectedVersion.Any,
				null,
				false,
				this);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new StorageMessage.CommitAck(InternalCorrId, _commitPosition, 500, 1, 1, true);
		}

		protected override Message When() {
			return new StorageMessage.RequestManagerTimerTick(DateTime.UtcNow + CommitTimeout + CommitTimeout);
		}

		[Test]
		public void failed_request_message_is_published() {
			Assert.That(Produced.ContainsSingle<StorageMessage.RequestCompleted>(
				x => x.CorrelationId == InternalCorrId && x.Success == false));
		}

		[Test]
		public void the_envelope_is_replied_to_with_failure() {
			Assert.That(Envelope.Replies.ContainsSingle<ClientMessage.DeleteStreamCompleted>(
				x => x.CorrelationId == ClientCorrId && x.Result == OperationResult.CommitTimeout));
		}
	}
}
