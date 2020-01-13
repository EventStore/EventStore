using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.Services.Replication;
using NUnit.Framework;
using DeleteStreamManager = EventStore.Core.Services.RequestManager.Managers.DeleteStream;

namespace EventStore.Core.Tests.Services.RequestManagement.DeleteMgr {
	[TestFixture]
	public class when_delete_stream_gets_stream_deleted : RequestManagerSpecification<DeleteStreamManager> {
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
				CommitSource);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield break;
		}

		protected override Message When() {
			return new StorageMessage.StreamDeleted(InternalCorrId);
		}

		[Test]
		public void failed_request_message_is_publised() {
			Assert.That(Produced.ContainsSingle<StorageMessage.RequestCompleted>(
				x => x.CorrelationId == InternalCorrId && x.Success == false));
		}

		[Test]
		public void the_envelope_is_replied_to_with_failure() {
			Assert.That(Envelope.Replies.ContainsSingle<ClientMessage.DeleteStreamCompleted>(
				x => x.CorrelationId == ClientCorrId && x.Result == OperationResult.StreamDeleted));
		}
	}
}
