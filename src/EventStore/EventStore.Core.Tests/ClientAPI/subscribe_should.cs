// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  
using System;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class subscribe_should: SpecificationWithDirectoryPerTestFixture
    {
        private const int Timeout = 10000;

        private MiniNode _node;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _node = new MiniNode(PathName);
            _node.Start();
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _node.Shutdown();
            base.TestFixtureTearDown();
        }

        [Test, Category("LongRunning")]
        public void be_able_to_subscribe_to_non_existing_stream_and_then_catch_created_event()
        {
            const string stream = "subscribe_should_be_able_to_subscribe_to_non_existing_stream_and_then_catch_created_event";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent, Position> eventAppeared = (x, p) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);

                var create = store.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.IsTrue(create.Wait(Timeout), "CreateStreamAsync timed out.");
                Assert.IsTrue(appeared.Wait(Timeout), "Event appeared countdown event timed out.");
            }
        }

        [Test, Category("LongRunning")]
        public void allow_multiple_subscriptions_to_same_stream()
        {
            const string stream = "subscribe_should_allow_multiple_subscriptions_to_same_stream";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var appeared = new CountdownEvent(2);
                var dropped = new CountdownEvent(2);

                Action<RecordedEvent, Position> eventAppeared = (x, p) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);
                store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);

                var create = store.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.IsTrue(create.Wait(Timeout), "CreateStreamAsync timed out.");

                Assert.IsTrue(appeared.Wait(Timeout), "Appeared countdown event timed out.");
            }
        }

        [Test, Category("LongRunning")]
        public void call_dropped_callback_after_unsubscribe_method_call()
        {
            const string stream = "subscribe_should_call_dropped_callback_after_unsubscribe_method_call";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var appeared =  new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent, Position> eventAppeared = (x, p) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);
                Assert.IsFalse(appeared.Wait(100), "Some event appeared!");

                store.UnsubscribeAsync(stream);
                Assert.IsTrue(dropped.Wait(Timeout), "Dropped countdown event timed out.");
            }
        }

        [Test, Category("LongRunning")]
        public void subscribe_to_deleted_stream_as_well_but_never_invoke_user_callbacks()
        {
            const string stream = "subscribe_should_subscribe_to_deleted_stream_as_well_but_never_invoke_user_callbacks";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent, Position> eventAppeared = (x, p) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                var create = store.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.IsTrue(create.Wait(Timeout), "CreateStreamAsync timed out.");
                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.IsTrue(delete.Wait(Timeout), "DeleteStreamAsync timed out.");

                var subscribe = store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);
                Assert.IsFalse(subscribe.Wait(100), "Something came on subscription!");
                Assert.IsFalse(dropped.Wait(100), "Subscription drop came!");
            }
        }

        [Test, Category("LongRunning")]
        public void not_call_dropped_if_stream_was_deleted()
        {
            const string stream = "subscribe_should_not_call_dropped_if_stream_was_deleted";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent, Position> eventAppeared = (x, p) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                var create = store.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.True(create.Wait(Timeout), "CreateStreamAsync timed out.");

                var subscribe = store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.IsTrue(delete.Wait(Timeout), "DeleteStreamAsync timed out.");

                Assert.IsTrue(appeared.Wait(Timeout), "Nothing appeared on subscription.");

                Assert.IsFalse(dropped.Wait(100), "Subscription drop notification came.");
                Assert.IsFalse(subscribe.Wait(100), "Subscription dropped.");
            }
        }

        [Test, Category("LongRunning")]
        public void catch_created_and_deleted_events_as_well()
        {
            const string stream = "subscribe_should_catch_created_and_deleted_events_as_well";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var appeared = new CountdownEvent(2);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent, Position> eventAppeared = (x, p) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);

                var create = store.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.IsTrue(create.Wait(Timeout), "CreateStreamAsync timed out.");
                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.IsTrue(delete.Wait(Timeout), "DeleteStreamAsync timed out.");

                Assert.IsTrue(appeared.Wait(Timeout), "Appeared countdown event timed out.");
            }
        }
    }
}
