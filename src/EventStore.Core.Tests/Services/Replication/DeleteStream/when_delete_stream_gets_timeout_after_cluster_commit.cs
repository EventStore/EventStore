using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.DeleteStream {
	public class when_delete_stream_gets_timeout_after_cluster_commit : RequestManagerSpecification {
		private long _commitPosition = 3000;
		protected override IRequestManager OnManager(FakePublisher publisher) {
			return new DeleteStreamRequestManager(publisher, CommitTimeout, false);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new ClientMessage.DeleteStream(InternalCorrId, ClientCorrId, Envelope, true, "test123",
				ExpectedVersion.Any, true, null);
			yield return new StorageMessage.CommitAck(InternalCorrId, _commitPosition, 500, 1, 1, true);
			yield return new CommitMessage.CommittedTo(_commitPosition);
		}

		protected override Message When() {
			return new StorageMessage.RequestManagerTimerTick(DateTime.UtcNow + CommitTimeout + CommitTimeout);
		}

		[Test]
		public void no_additional_messages_are_published() {
			Assert.That(Produced.Count == 0);
		}
		[Test]
		public void the_envelope_has_single_successful_reply() {
			Assert.AreEqual(1, Envelope.Replies.Count);
			Assert.AreEqual(OperationResult.Success, ((ClientMessage.DeleteStreamCompleted)Envelope.Replies[0]).Result);
		}
	}
}
