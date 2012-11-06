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

                Action<RecordedEvent, Position> eventAppeared = (x, p) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                store.SubscribeToAllStreamsAsync(eventAppeared, subscriptionDropped);
                store.SubscribeToAllStreamsAsync(eventAppeared, subscriptionDropped);

                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.That(create.Wait(Timeout));

                Assert.That(appeared.Wait(Timeout));
            }
        }

        [Test]
        public void drop_all_global_subscribers_when_unsubscribe_from_all_called()
        {
            using (var store = new EventStoreConnection(Node.TcpEndPoint))
            {
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(2);

                Action<RecordedEvent, Position> eventAppeared = (x, p) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                store.SubscribeToAllStreamsAsync(eventAppeared, subscriptionDropped);
                store.SubscribeToAllStreamsAsync(eventAppeared, subscriptionDropped);

                store.UnsubscribeFromAllStreams();
                Assert.That(dropped.Wait(Timeout));
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

                Action<RecordedEvent, Position> eventAppeared = (x, p) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                store.SubscribeToAllStreamsAsync(eventAppeared, subscriptionDropped);

                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.That(create.Wait(Timeout));
                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.That(delete.Wait(Timeout));

                Assert.That(appeared.Wait(Timeout));
            }
        }
    }
}
