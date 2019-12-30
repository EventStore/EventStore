using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using WriteEventsMgr = EventStore.Core.Services.RequestManager.Managers.WriteEvents;

namespace EventStore.Core.Tests.Services.Replication.WriteStream {
	[TestFixture]
	public class when_write_stream_completes_successfully : RequestManagerSpecification<WriteEventsMgr> {
		private long _prepareLogPosition = 100;
		private long _commitLogPosition = 100;

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
				new[] { DummyEvent() });
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new StorageMessage.PrepareAck(InternalCorrId, _prepareLogPosition, PrepareFlags.SingleWrite|PrepareFlags.Data);
			yield return new StorageMessage.CommitAck(InternalCorrId, _commitLogPosition, 2, 3, 3);
		}

		protected override Message When() {
			return new CommitMessage.CommittedTo(_commitLogPosition);
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
