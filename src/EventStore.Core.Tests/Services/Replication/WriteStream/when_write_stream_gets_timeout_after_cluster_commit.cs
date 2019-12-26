using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.WriteStream {
	[TestFixture]
	public class when_write_stream_gets_timeout_after_cluster_commit : RequestManagerSpecification {
		long _commitPosition = 100;
		protected override IRequestManager OnManager(FakePublisher publisher) {
			return new WriteStreamRequestManager(publisher,  CommitTimeout, false);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new ClientMessage.WriteEvents(InternalCorrId, ClientCorrId, Envelope, true, "test123",
				ExpectedVersion.Any, new[] {DummyEvent()}, null);
			yield return new StorageMessage.CommitAck(InternalCorrId, _commitPosition, 1, 0, 0);
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
			Assert.AreEqual(OperationResult.Success, ((ClientMessage.WriteEventsCompleted)Envelope.Replies[0]).Result);
		}
	}
}
