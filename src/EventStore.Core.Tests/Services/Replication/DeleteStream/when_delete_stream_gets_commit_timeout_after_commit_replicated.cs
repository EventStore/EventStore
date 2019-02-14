using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.DeleteStream {
	[TestFixture]
	public class when_delete_stream_gets_commit_timeout_after_commit_replicated : RequestManagerSpecification {
		protected override TwoPhaseRequestManagerBase OnManager(FakePublisher publisher) {
			return new DeleteStreamTwoPhaseRequestManager(publisher, 3, PrepareTimeout, CommitTimeout, false);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new ClientMessage.DeleteStream(InternalCorrId, ClientCorrId, Envelope, true, "test123",
				ExpectedVersion.Any, true, null);
			yield return new StorageMessage.CommitReplicated(InternalCorrId, 1, 1, 0, 0);
		}

		protected override Message When() {
			return new StorageMessage.RequestManagerTimerTick(
				DateTime.UtcNow + TimeSpan.FromTicks(CommitTimeout.Ticks / 2));
		}

		[Test]
		public void no_messages_are_published() {
			Assert.That(Produced.Count == 0);
		}
	}
}
