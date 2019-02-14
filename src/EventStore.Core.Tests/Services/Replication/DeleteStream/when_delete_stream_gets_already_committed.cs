using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.DeleteStream {
	[TestFixture]
	public class when_delete_stream_gets_already_committed : RequestManagerSpecification {
		protected override TwoPhaseRequestManagerBase OnManager(FakePublisher publisher) {
			return new DeleteStreamTwoPhaseRequestManager(publisher, 3, PrepareTimeout, CommitTimeout, false);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new ClientMessage.DeleteStream(InternalCorrId, ClientCorrId, Envelope, true, "test123",
				ExpectedVersion.Any, true, null);
		}

		protected override Message When() {
			return new StorageMessage.AlreadyCommitted(InternalCorrId, "test123", 0, 1);
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
