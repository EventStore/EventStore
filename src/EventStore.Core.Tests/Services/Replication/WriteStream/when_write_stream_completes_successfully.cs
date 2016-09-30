using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.WriteStream
{
    [TestFixture]
    public class when_write_stream_completes_successfully : RequestManagerSpecification
    {
        protected override TwoPhaseRequestManagerBase OnManager(FakePublisher publisher)
        {
            return new WriteStreamTwoPhaseRequestManager(publisher, 3, 3, PrepareTimeout, CommitTimeout, false);
        }

        protected override IEnumerable<Message> WithInitialMessages()
        {
            yield return new ClientMessage.WriteEvents(InternalCorrId, ClientCorrId, Envelope, true, "test123", ExpectedVersion.Any, new[] { DummyEvent() }, null);
//            yield return new StorageMessage.PrepareAck(InternalCorrId, 1, PrepareFlags.StreamDelete);
//            yield return new StorageMessage.PrepareAck(InternalCorrId, 1, PrepareFlags.StreamDelete);
//            yield return new StorageMessage.PrepareAck(InternalCorrId, 1, PrepareFlags.StreamDelete);
            yield return new StorageMessage.CommitAck(InternalCorrId, 100, 2, 3, 3);
            yield return new StorageMessage.CommitAck(InternalCorrId, 100, 2, 3, 3, true);

        }

        protected override Message When()
        {
            return new StorageMessage.CommitAck(InternalCorrId, 100, 2, 3, 3);
        }

        [Test]
        public void successful_request_message_is_publised()
        {
            Assert.That(Produced.ContainsSingle<StorageMessage.RequestCompleted>(
                x => x.CorrelationId == InternalCorrId && x.Success));
        }

        [Test]
        public void the_envelope_is_replied_to_with_success()
        {
            Assert.That(Envelope.Replies.ContainsSingle<ClientMessage.WriteEventsCompleted>(
                x => x.CorrelationId == ClientCorrId && x.Result == OperationResult.Success));
        }
    }
}