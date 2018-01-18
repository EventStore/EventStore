using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.DeleteStream
{
    [TestFixture]
    public class when_delete_stream_gets_commit_timeout_before_commit_replicated : RequestManagerSpecification
    {
        protected override TwoPhaseRequestManagerBase OnManager(FakePublisher publisher)
        {
            return new DeleteStreamTwoPhaseRequestManager(publisher, 3, PrepareTimeout, CommitTimeout, false);
        }

        protected override IEnumerable<Message> WithInitialMessages()
        {
            yield return new ClientMessage.DeleteStream(InternalCorrId, ClientCorrId, Envelope, true, "test123", ExpectedVersion.Any, true, null);
        }

        protected override Message When()
        {
            return new StorageMessage.RequestManagerTimerTick(DateTime.UtcNow + CommitTimeout + TimeSpan.FromMinutes(5));
        }

        [Test]
        public void failed_request_message_is_publised()
        {
            Assert.That(Produced.ContainsSingle<StorageMessage.RequestCompleted>(
                x => x.CorrelationId == InternalCorrId && x.Success == false));
        }

        [Test]
        public void the_envelope_is_replied_to_with_failure()
        {
            Assert.That(Envelope.Replies.ContainsSingle<ClientMessage.DeleteStreamCompleted>(
                x => x.CorrelationId == ClientCorrId && x.Result == OperationResult.CommitTimeout));
        }
    }
}