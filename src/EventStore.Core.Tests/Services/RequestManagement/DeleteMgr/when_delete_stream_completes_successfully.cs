using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.Services.Replication;
using NUnit.Framework;
using EventStore.Core.Services.RequestManager.Managers;

namespace EventStore.Core.Tests.Services.RequestManagement.DeleteMgr {
	[TestFixture]
	public class when_delete_stream_completes_successfully : RequestManagerSpecification<DeleteStream> {
		private long commitPosition = 1000;
		private readonly string streamId = $"DeleteTest-{Guid.NewGuid()}";
		protected override DeleteStream OnManager(FakePublisher publisher) {
			return new DeleteStream(
				publisher,
				CommitTimeout,
				Envelope,
				InternalCorrId,
				ClientCorrId,
			   	streamId: streamId,
				betterOrdering: true,
				expectedVersion: ExpectedVersion.Any,
				user: null,
				hardDelete: false,
				commitSource: CommitSource);
		}

		protected override IEnumerable<Message> WithInitialMessages() {
			yield return new StorageMessage.CheckStreamAccessCompleted(InternalCorrId, streamId, null, StreamAccessType.Delete, new StreamAccess(true));
			yield return new StorageMessage.CommitAck(InternalCorrId, commitPosition, 2, 3, 3);
			}

		protected override Message When() {
			return new ReplicationTrackingMessage.IndexedTo(commitPosition);
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
