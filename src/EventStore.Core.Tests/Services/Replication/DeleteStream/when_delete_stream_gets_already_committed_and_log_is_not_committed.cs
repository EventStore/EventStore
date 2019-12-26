using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.DeleteStream {
	[TestFixture]
	public class when_delete_stream_gets_already_committed_and_log_is_not_committed : RequestManagerSpecification {
		private long _commitPosition = 3000;
		protected override IRequestManager OnManager(FakePublisher publisher) {
			return new DeleteStreamRequestManager(publisher, CommitTimeout, false);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new ClientMessage.DeleteStream(InternalCorrId, ClientCorrId, Envelope, true, "test123",
				ExpectedVersion.Any, true, null);
			yield return new CommitMessage.CommittedTo(_commitPosition -1);
		}

		protected override Message When() {
			return new StorageMessage.AlreadyCommitted(InternalCorrId, "test123", 0, 1, _commitPosition);
		}

		[Test]
		public void no_messages_are_published() {
			Assert.That(!Produced.Any());
		}

		[Test]
		public void the_envelope_is_not_replied_to() {
			Assert.That(!Envelope.Replies.Any());
		}
	}
}
