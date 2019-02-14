using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.WriteStream {
	[TestFixture, Ignore("DeleteStream operation is not 2-phase now, it does not expect PrepareAck anymore.")]
	public class when_write_stream_gets_prepare_timeout_after_prepares : RequestManagerSpecification {
		protected override TwoPhaseRequestManagerBase OnManager(FakePublisher publisher) {
			return new WriteStreamTwoPhaseRequestManager(publisher, 3, PrepareTimeout, CommitTimeout, false);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new ClientMessage.WriteEvents(InternalCorrId, ClientCorrId, Envelope, true, "test123",
				ExpectedVersion.Any, new[] {DummyEvent()}, null);
			yield return new StorageMessage.PrepareAck(InternalCorrId, 1, PrepareFlags.SingleWrite);
			yield return new StorageMessage.PrepareAck(InternalCorrId, 1, PrepareFlags.SingleWrite);
			yield return new StorageMessage.PrepareAck(InternalCorrId, 1, PrepareFlags.SingleWrite);
		}

		protected override Message When() {
			return new StorageMessage.RequestManagerTimerTick(
				DateTime.UtcNow + TimeSpan.FromTicks(CommitTimeout.Ticks / 2));
		}

		[Test]
		public void no_messages_are_published() {
			Assert.That(Produced.Count == 0);
		}

		[Test]
		public void the_envelope_is_not_replied_to() {
			Assert.AreEqual(0, Envelope.Replies.Count);
		}
	}
}
