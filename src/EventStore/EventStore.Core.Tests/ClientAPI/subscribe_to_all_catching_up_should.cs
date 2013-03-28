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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.Common.Log;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;
using ILogger = EventStore.Common.Log.ILogger;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class subscribe_to_all_catching_up_should : SpecificationWithDirectory
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<subscribe_to_all_catching_up_should>();
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

        private MiniNode _node;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            _node = new MiniNode(PathName);
            _node.Start();
        }

        [TearDown]
        public override void TearDown()
        {
            _node.Shutdown();
            base.TearDown();
        }
        
        [Test, Category("LongRunning")]
        public void call_dropped_callback_after_stop_method_call()
        {
            using (var store = TestConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);

                var dropped = new CountdownEvent(1);
                var subscription = store.SubscribeToAllFrom(null, false, (x, y) => { }, (x, y, z) => dropped.Signal());

                Assert.IsFalse(dropped.Wait(0));
                subscription.Stop(Timeout);
                Assert.IsTrue(dropped.Wait(Timeout));
            }
        }


        [Test, Category("LongRunning")]
        public void be_able_to_subscribe_to_empty_db()
        {
            using (var store = TestConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var appeared = new ManualResetEventSlim(false);
                var dropped = new CountdownEvent(1);

                var subscription = store.SubscribeToAllFrom(null, false, (_, x) => appeared.Set(), (_, __, ___) => dropped.Signal());

                Thread.Sleep(100); // give time for first pull phase
                var dummySubscr = store.SubscribeToAll(false, x => { }, () => { }).Result; // wait for dummy subscription to complete
                Thread.Sleep(100);

                Assert.IsFalse(appeared.Wait(0), "Some event appeared!");
                Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
                subscription.Stop(Timeout);
                Assert.IsTrue(dropped.Wait(Timeout));
            }
        }

        [Test, Category("LongRunning")]
        public void read_all_existing_events_and_keep_listening_to_new_ones()
        {
            using (var store = TestConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);

                var events = new List<ResolvedEvent>();
                var appeared = new CountdownEvent(20);
                var dropped = new CountdownEvent(1);

                for (int i = 0; i < 10; ++i)
                {
                    store.AppendToStream("stream-" + i.ToString(), -1, new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null));
                }

                var subscription = store.SubscribeToAllFrom(null,
                                                            false,
                                                            (x, y) =>
                                                            {
                                                                events.Add(y);
                                                                appeared.Signal();
                                                            },
                                                            (x, y, z) => dropped.Signal());
                for (int i = 10; i < 20; ++i)
                {
                    store.AppendToStream("stream-" + i.ToString(), -1, new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null));
                }

                if (!appeared.Wait(Timeout))
                {
                    Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
                    Assert.Fail("Couldn't wait for all events.");
                }

                Assert.AreEqual(20, events.Count); 
                for (int i = 0; i < 20; ++i)
                {
                    Assert.AreEqual("et-" + i.ToString(), events[i].OriginalEvent.EventType);
                }

                Assert.IsFalse(dropped.Wait(0));
                subscription.Stop(Timeout);
                Assert.IsTrue(dropped.Wait(Timeout));
            }
        }

        [Test, Category("LongRunning")]
        public void filter_events_and_keep_listening_to_new_ones()
        {
            using (var store = TestConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);

                var events = new List<ResolvedEvent>();
                var appeared = new CountdownEvent(10);
                var dropped = new CountdownEvent(1);

                for (int i = 0; i < 10; ++i)
                {
                    store.AppendToStream("stream-" + i.ToString(), -1, new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null));
                }

                var allSlice = store.ReadAllEventsForward(Position.Start, 100, false);
                var lastEvent = allSlice.Events.Last();

                var subscription = store.SubscribeToAllFrom(
                    lastEvent.OriginalPosition,
                    false,
                    (x, y) =>
                    {
                        events.Add(y);
                        appeared.Signal();
                    },
                    (x, y, z) =>
                    {
                        Log.Info("Subscription dropped: {0}, {1}.", y, z);
                        dropped.Signal();
                    });

                for (int i = 10; i < 20; ++i)
                {
                    store.AppendToStream("stream-" + i.ToString(), -1, new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null));
                }
                Log.Info("Waiting for events...");
                if (!appeared.Wait(Timeout))
                {
                    Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
                    Assert.Fail("Couldn't wait for all events.");
                }
                Log.Info("Events appeared...");
                Assert.AreEqual(10, events.Count);
                for (int i = 0; i < 10; ++i)
                {
                    Assert.AreEqual("et-" + (10 + i).ToString(), events[i].OriginalEvent.EventType);
                }

                Assert.IsFalse(dropped.Wait(0));
                subscription.Stop(Timeout);
                Assert.IsTrue(dropped.Wait(Timeout));

                Assert.AreEqual(events.Last().OriginalPosition, subscription.LastProcessedPosition);
            }
        }

        [Test, Category("LongRunning")]
        public void filter_events_and_work_if_nothing_was_written_after_subscription()
        {
            using (var store = TestConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);

                var events = new List<ResolvedEvent>();
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                for (int i = 0; i < 10; ++i)
                {
                    store.AppendToStream("stream-" + i.ToString(), -1, new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null));
                }

                var allSlice = store.ReadAllEventsForward(Position.Start, 100, false);
                var lastEvent = allSlice.Events[allSlice.Events.Length - 2];

                var subscription = store.SubscribeToAllFrom(
                    lastEvent.OriginalPosition,
                    false,
                    (x, y) =>
                    {
                        events.Add(y);
                        appeared.Signal();
                    },
                    (x, y, z) =>
                    {
                        Log.Info("Subscription dropped: {0}, {1}.", y, z);
                        dropped.Signal();
                    });

                Log.Info("Waiting for events...");
                if (!appeared.Wait(Timeout))
                {
                    Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
                    Assert.Fail("Couldn't wait for all events.");
                }
                Log.Info("Events appeared...");
                Assert.AreEqual(1, events.Count);
                Assert.AreEqual("et-9", events[0].OriginalEvent.EventType);

                Assert.IsFalse(dropped.Wait(0));
                subscription.Stop(Timeout);
                Assert.IsTrue(dropped.Wait(Timeout));

                Assert.AreEqual(events.Last().OriginalPosition, subscription.LastProcessedPosition);
            }
        }
    }
}
