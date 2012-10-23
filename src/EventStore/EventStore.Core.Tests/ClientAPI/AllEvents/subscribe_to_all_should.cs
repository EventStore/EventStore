using System;
using System.Linq;
using System.Threading;
using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.AllEvents
{
    [TestFixture, Category("LongRunning"), Explicit]
    internal class subscribe_to_all_should
    {
        public int Timeout = 1000;
        public MiniNode Node;

        [SetUp]
        public void SetUp()
        {
            Node = MiniNode.Create(8111, 9111);
            Node.Start();
        }

        [TearDown]
        public void TearDown()
        {
            Node.Shutdown();
        }

        [Test]
        public void allow_multiple_subscriptions()
        {
            const string stream = "subscribe_to_all_should_allow_multiple_subscriptions";
            using (var store = new EventStoreConnection(Node.TcpEndPoint))
            {
                var appeared = new CountdownEvent(2);
                var dropped = new CountdownEvent(2);

                Action<RecordedEvent> eventAppeared = (x) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                var subscribe1 = store.SubscribeToAllStreamsAsync(eventAppeared, subscriptionDropped);
                var subscribe2 = store.SubscribeToAllStreamsAsync(eventAppeared, subscriptionDropped);
                var create = store.CreateStreamAsync(stream, new byte[0]);

                Assert.That(create.Wait(Timeout));
                Assert.That(appeared.Wait(Timeout));

                store.UnsubscribeFromAllStreams();
                Assert.That(dropped.Wait(Timeout));

                Assert.That(subscribe1.Wait(Timeout));
                Assert.That(subscribe2.Wait(Timeout));
            }
        }

        [Test]
        public void drop_all_global_subscribers_when_unsubscribe_from_all_called()
        {
            using (var store = new EventStoreConnection(Node.TcpEndPoint))
            {
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(2);

                Action<RecordedEvent> eventAppeared = (x) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                var subscribe1 = store.SubscribeToAllStreamsAsync(eventAppeared, subscriptionDropped);
                var subscribe2 = store.SubscribeToAllStreamsAsync(eventAppeared, subscriptionDropped);

                store.UnsubscribeFromAllStreams();
                Assert.That(dropped.Wait(Timeout));

                Assert.That(!appeared.Wait(50));

                Assert.That(subscribe1.Wait(Timeout));
                Assert.That(subscribe2.Wait(Timeout));
            }
        }

        [Test]
        public void catch_created_and_deleted_events_as_well()
        {
            const string stream = "subscribe_to_all_should_catch_created_and_deleted_events_as_well";
            using (var store = new EventStoreConnection(Node.TcpEndPoint))
            {
                var appeared = new CountdownEvent(2);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent> eventAppeared = (x) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                var subscribe = store.SubscribeToAllStreamsAsync(eventAppeared, subscriptionDropped);

                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.That(create.Wait(Timeout));
                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.That(delete.Wait(Timeout));

                Assert.That(appeared.Wait(Timeout));

                store.UnsubscribeFromAllStreams();
                Assert.That(dropped.Wait(Timeout));
                Assert.That(subscribe.Wait(Timeout));
            }
        }

        [Test]
        public void not_receive_any_events_after_unsubscribe_from_all_method_call()
        {
            const string stream = "subscribe_to_all_should_not_receive_any_events_after_unsubscribe_from_all_method_call";
            using (var store = new EventStoreConnection(Node.TcpEndPoint))
            {
                var appeared = new CountdownEvent(3);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent> eventAppeared = (x) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                var subscribe = store.SubscribeToAllStreamsAsync(eventAppeared, subscriptionDropped);

                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.That(create.Wait(Timeout));
                var testEvents = Enumerable.Range(0, 2).Select(x => new TestEvent((x + 1).ToString()));
                var write = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.That(write.Wait(Timeout));

                store.UnsubscribeFromAllStreams();
                Assert.That(dropped.Wait(Timeout));
                Assert.That(subscribe.Wait(Timeout));

                var write2 = store.AppendToStreamAsync(stream, 2, testEvents);
                Assert.That(write2.Wait(Timeout));

                Assert.That(appeared.Wait(Timeout));
            }
        }
    }
}
