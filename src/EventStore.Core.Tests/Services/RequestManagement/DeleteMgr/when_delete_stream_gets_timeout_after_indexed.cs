using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using NUnit.Framework;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Bus.Helpers;
using System.Threading;
using EventStore.Core.Tests.Helpers;

namespace EventStore.Core.Tests.Services.RequestManagement.DeleteMgr {
	public class when_delete_stream_gets_timeout_after_indexed : RequestManagerSpecification<DeleteStream> {
		private long _commitPosition = 3000;
		protected override DeleteStream OnManager(FakePublisher publisher) {
			return new DeleteStream(
				publisher,
				1,
				CommitTimeout, 
				Envelope,
				InternalCorrId,
				ClientCorrId,
				"test123",
				ExpectedVersion.Any,
				false,
				CommitSource);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new StorageMessage.CommitIndexed(InternalCorrId, _commitPosition, 500, 1, 1);
			yield return new ReplicationTrackingMessage.ReplicatedTo(_commitPosition);
			yield return new ReplicationTrackingMessage.IndexedTo(_commitPosition);
		}

		protected override Message When() {
			AssertEx.IsOrBecomesTrue(() => Manager.Result == OperationResult.Success, TimeSpan.FromSeconds(1));			
			Manager.CancelRequest();
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
				Assert.That(Envelope.Replies.ContainsSingle<ClientMessage.DeleteStreamCompleted>(
				x => x.CorrelationId == ClientCorrId && x.Result == OperationResult.Success));
			} 
		}
	}
}
