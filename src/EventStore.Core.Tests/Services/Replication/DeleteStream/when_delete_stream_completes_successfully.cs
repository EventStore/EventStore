using System.Collections.Generic;
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
	public class when_delete_stream_completes_successfully : RequestManagerSpecification {
		protected override IRequestManager OnManager(FakePublisher publisher) {
			return new DeleteStreamRequestManager(publisher, CommitTimeout, false);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new ClientMessage.DeleteStream(InternalCorrId, ClientCorrId, Envelope, true, "test123",
				ExpectedVersion.Any, true, null);
			yield return new StorageMessage.CommitAck(InternalCorrId, 100, 2, 3, 3);
		}

		protected override Message When() {
			return new CommitMessage.CommittedTo(100);
		}

		[Test]
		public void successful_request_message_is_publised() {
			Assert.That(Produced.ContainsSingle<StorageMessage.RequestCompleted>(
				x => x.CorrelationId == InternalCorrId && x.Success));
		}

		[Test]
		public void the_envelope_is_replied_to_with_success() {
			Assert.That(Envelope.Replies.ContainsSingle<ClientMessage.DeleteStreamCompleted>(
				x => x.CorrelationId == ClientCorrId && x.Result == OperationResult.Success));
		}
	}
}
