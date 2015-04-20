using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services.Replication;
using NUnit.Framework;

namespace EventStore.Core.Tests.Messaging
{

    [TestFixture]
    public class RequestResponseDispatcherTests
    {
        [Test]
        public void lock_is_not_held_when_calling_unknown_callbacks()
        {
            var requestResponseDispatcher = new RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>(
                new FakePublisher(), _ => _.CorrelationId, _ => _.CorrelationId, new FakeEnvelope());
            object aSecondLock = new object();

            //Control flow events to simulate race condition
            var lock1EnteredEvent = new ManualResetEventSlim();
            var lock2EnteredEvent = new ManualResetEventSlim();

            var request1 = new ClientMessage.WriteEvents(Guid.NewGuid(), Guid.NewGuid(), new FakeEnvelope(), false, "stream", 0, new Event[0], null);
            var response1 = new ClientMessage.WriteEventsCompleted(request1.CorrelationId, OperationResult.AccessDenied, "Test");
            var request2 = new ClientMessage.WriteEvents(Guid.NewGuid(), Guid.NewGuid(), new FakeEnvelope(), false, "stream", 0, new Event[0], null);

            //Set up callback for first request.
            requestResponseDispatcher.Publish(request1, _ =>
            {
                lock2EnteredEvent.Set();

                lock (aSecondLock)
                {

                }
            });

            var theRequest1Handler = Task.Factory.StartNew(() =>
            {
                lock1EnteredEvent.Wait();
                requestResponseDispatcher.Handle(response1);
            });

            var theRequest2Publisher = Task.Factory.StartNew(() =>
            {
                lock (aSecondLock)
                {
                    lock1EnteredEvent.Set();
                    lock2EnteredEvent.Wait();

                    requestResponseDispatcher.Publish(request2, _ => { });
                }
            });

            Assert.IsTrue(Task.WaitAll(new[] { theRequest1Handler, theRequest2Publisher }, TimeSpan.FromSeconds(5)));
        }
    }
}
