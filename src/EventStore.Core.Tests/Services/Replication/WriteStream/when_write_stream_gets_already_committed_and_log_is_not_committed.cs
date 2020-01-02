using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using WriteEventsMgr =EventStore.Core.Services.RequestManager.Managers.WriteEvents;

namespace EventStore.Core.Tests.Services.Replication.WriteStream {
	[TestFixture]
	public class when_write_stream_gets_already_committed_and_log_is_not_committed : RequestManagerSpecification<WriteEventsMgr> {
		private long _commitLogPosition = 1000;
		protected override WriteEventsMgr OnManager(FakePublisher publisher) {
			return new WriteEventsMgr(
				publisher, 
				CommitTimeout, 
				Envelope,
				InternalCorrId,
				ClientCorrId,
				"test123",
				true,
				ExpectedVersion.Any,
				null,
				new[] {DummyEvent()},
				this);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new CommitMessage.CommittedTo(_commitLogPosition - 1);
		}

		protected override Message When() {
			return new StorageMessage.AlreadyCommitted(InternalCorrId, "test123", 0, 1, _commitLogPosition);
		}

		[Test]
		public void successful_request_message_is_publised() {
			Assert.That(Produced.ContainsSingle<StorageMessage.RequestCompleted>(
				x => x.CorrelationId == InternalCorrId && x.Success));
		}

		[Test]
		public void the_envelope_is_replied_to_with_success() {
			Assert.That(Envelope.Replies.ContainsSingle<ClientMessage.WriteEventsCompleted>(
				x => x.CorrelationId == ClientCorrId && x.Result == OperationResult.Success));
		}
	}
}
