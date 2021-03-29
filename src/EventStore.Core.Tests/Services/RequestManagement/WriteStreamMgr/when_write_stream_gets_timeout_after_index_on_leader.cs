using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.Tests.Helpers;

namespace EventStore.Core.Tests.Services.RequestManagement.WriteStreamMgr {
	[TestFixture]
	public class when_write_stream_gets_timeout_after_index_on_leader : RequestManagerSpecification<WriteEvents> {
		private long _prepareLogPosition = 100;
		private long _commitPosition = 100;
		protected override WriteEvents OnManager(FakePublisher publisher) {
			return new WriteEvents(
				publisher,
				1,
				CommitTimeout,
				Envelope,
				InternalCorrId,
				ClientCorrId,
				"test123",
				ExpectedVersion.Any,
				new[] { DummyEvent() },
				CommitSource);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new StorageMessage.PrepareAck(InternalCorrId, _prepareLogPosition, PrepareFlags.SingleWrite | PrepareFlags.Data);
			yield return new StorageMessage.CommitIndexed(InternalCorrId, _commitPosition, 1, 0, 0);
			yield return new ReplicationTrackingMessage.ReplicatedTo(_commitPosition);
			yield return new ReplicationTrackingMessage.IndexedTo(_commitPosition);
		}

		protected override Message When() {
			AssertEx.IsOrBecomesTrue(() => Manager.Result == OperationResult.Success, TimeSpan.FromSeconds(1));
			Manager.PhaseTimeout(2);
			return new TestMessage();
		}

		[Test]
		public void no_failure_messages_are_published() {			
			if (Produced.Count > 0) {
				Assert.AreEqual(true, ((StorageMessage.RequestCompleted)Produced[0]).Success);
			}
		}
		[Test]
		public void the_envelope_does_not_contain_failure() {
			if (Envelope.Replies.Count > 0) {
				Assert.That(Envelope.Replies.ContainsSingle<ClientMessage.WriteEventsCompleted>(
				x => x.CorrelationId == ClientCorrId && x.Result == OperationResult.Success));
			} 
		}
	}
}
