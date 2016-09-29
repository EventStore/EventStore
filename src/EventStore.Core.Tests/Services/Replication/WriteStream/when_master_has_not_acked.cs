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
    public class when_master_has_not_acked : RequestManagerSpecification
    {
        protected override TwoPhaseRequestManagerBase OnManager(FakePublisher publisher)
        {
            return new WriteStreamTwoPhaseRequestManager(publisher, 3, 3, PrepareTimeout, CommitTimeout, false);
        }

        protected override IEnumerable<Message> WithInitialMessages()
        {
            yield return new ClientMessage.WriteEvents(InternalCorrId, ClientCorrId, Envelope, true, "test123", ExpectedVersion.Any, new[] { DummyEvent() }, null);
            yield return new StorageMessage.CommitAck(InternalCorrId, 100, 2, 3, 3);
        }

        protected override Message When()
        {
            return new StorageMessage.CommitAck(InternalCorrId, 100, 2, 3, 3);
        }

        [Test]
        public void no_messages_are_published()
        {
            Assert.That(Produced.Count == 0);
        }
    }
}