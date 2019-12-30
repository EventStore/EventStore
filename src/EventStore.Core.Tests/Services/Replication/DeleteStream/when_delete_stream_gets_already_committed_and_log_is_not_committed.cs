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
using DeleteStreamManager = EventStore.Core.Services.RequestManager.Managers.DeleteStream;

namespace EventStore.Core.Tests.Services.Replication.DeleteStream {
	[TestFixture]
	public class when_delete_stream_gets_already_committed_and_log_is_not_committed : RequestManagerSpecification<DeleteStreamManager> {
		private long _commitPosition = 3000;
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
				false);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
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
