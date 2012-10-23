using System;
using System.Linq;
using System.Threading;
using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture]
    internal class subscribe_should
    {
        public int Timeout = 1000;

        [Test]
        public void be_able_to_subscribe_to_non_existing_stream_and_then_catch_created_event()
        {
            const string stream = "subscribe_should_be_able_to_subscribe_to_non_existing_stream_and_then_catch_created_event";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent> eventAppeared = (x) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                var subscribe = store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);
                var create = store.CreateStreamAsync(stream, new byte[0]);

                Assert.That(create.Wait(Timeout));
                Assert.That(appeared.Wait(Timeout));

                store.Unsubscribe(stream);
                Assert.That(dropped.Wait(Timeout));
                Assert.That(subscribe.Wait(Timeout));
            }
        }

        [Test]
        public void allow_multiple_subscriptions_to_same_stream()
        {
            const string stream = "subscribe_should_allow_multiple_subscriptions_to_same_stream";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var appeared = new CountdownEvent(2);
                var dropped = new CountdownEvent(2);

                Action<RecordedEvent> eventAppeared = (x) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                var subscribe1 = store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);
                var subscribe2 = store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);
                var create = store.CreateStreamAsync(stream, new byte[0]);

                Assert.That(create.Wait(Timeout));
                Assert.That(appeared.Wait(Timeout));

                store.Unsubscribe(stream);
                Assert.That(dropped.Wait(Timeout));

                Assert.That(subscribe1.Wait(Timeout));
                Assert.That(subscribe2.Wait(Timeout));
            }
        }

        [Test]
        public void call_dropped_callback_after_unsubscribe_method_call()
        {
            const string stream = "subscribe_should_call_dropped_callback_after_unsubscribe_method_call";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var appeared =  new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent> eventAppeared = (x) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                var subscribe = store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);
                Assert.That(!appeared.Wait(50));

                store.Unsubscribe(stream);
                Assert.That(dropped.Wait(Timeout));
                Assert.That(subscribe.Wait(Timeout));
            }
        }

        [Test]
        public void subscribe_to_deleted_stream_as_well_but_never_invoke_user_callbacks()
        {
            const string stream = "subscribe_should_subscribe_to_deleted_stream_as_well_but_never_invoke_user_callbacks";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent> eventAppeared = (x) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.That(create.Wait(Timeout));
                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.That(delete.Wait(Timeout));

                var subscribe = store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);
                Assert.That(!subscribe.Wait(50));
                Assert.That(!dropped.Wait(50));
            }
        }

        [Test]
        public void not_call_dropped_if_stream_was_deleted()
        {
            const string stream = "subscribe_should_not_call_dropped_if_stream_was_deleted";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent> eventAppeared = (x) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.That(create.Wait(Timeout));

                var subscribe = store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.That(delete.Wait(Timeout));

                Assert.That(appeared.Wait(Timeout));

                Assert.That(!dropped.Wait(50));
                Assert.That(!subscribe.Wait(50));
            }
        }

        [Test]
        public void catch_created_and_deleted_events_as_well()
        {
            const string stream = "subscribe_should_catch_created_and_deleted_events_as_well";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var appeared = new CountdownEvent(2);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent> eventAppeared = (x) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                var subscribe = store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);

                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.That(create.Wait(Timeout));
                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.That(delete.Wait(Timeout));

                Assert.That(appeared.Wait(Timeout));

                store.Unsubscribe(stream);
                Assert.That(dropped.Wait(Timeout));
                Assert.That(subscribe.Wait(Timeout));
            }
        }

        [Test]
        public void not_receive_any_events_after_unsubscribe_method_call()
        {
            const string stream = "subscribe_should_not_receive_any_events_after_unsubscribe_method_call";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var appeared = new CountdownEvent(11);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent> eventAppeared = x => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                var subscribe = store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);

                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.That(create.Wait(Timeout));
                var testEvents = Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())).ToArray();
                var write = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.That(write.Wait(Timeout));

                Assert.That(appeared.Wait(Timeout));
                store.Unsubscribe(stream);

                Assert.That(dropped.Wait(Timeout));
                Assert.That(subscribe.Wait(Timeout));

                var write2 = store.AppendToStreamAsync(stream, 10, testEvents);
                Assert.That(write2.Wait(Timeout));
            }
        }
    }
}
