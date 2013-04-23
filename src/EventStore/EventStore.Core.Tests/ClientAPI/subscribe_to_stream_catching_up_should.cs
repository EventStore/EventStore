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
    public class subscribe_to_stream_catching_up_should : SpecificationWithDirectoryPerTestFixture
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<subscribe_to_stream_catching_up_should>();
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

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
        public void be_able_to_subscribe_to_non_existing_stream()
        {
            const string stream = "be_able_to_subscribe_to_non_existing_stream";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();
                var appeared = new ManualResetEventSlim(false);
                var dropped = new CountdownEvent(1);

                var subscription = store.SubscribeToStreamFrom(stream,
                                                               null,
                                                               false,
                                                               (_, x) => appeared.Set(),
                                                               _ => Log.Info("Live processing started."),
                                                               (_, __, ___) => dropped.Signal());

                Thread.Sleep(100); // give time for first pull phase
                var dummySubscr = store.SubscribeToStream(stream, false, (s, x) => { }, (s, r, e) => { }).Result; // wait for dummy subscription to complete
                Thread.Sleep(100);
                Assert.IsFalse(appeared.Wait(0), "Some event appeared!");
                Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
                subscription.Stop(Timeout);
                Assert.IsTrue(dropped.Wait(Timeout));
            }
        }

        [Test, Category("LongRunning")]
        public void be_able_to_subscribe_to_non_existing_stream_and_then_catch_event()
        {
            const string stream = "be_able_to_subscribe_to_non_existing_stream_and_then_catch_event";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                var subscription = store.SubscribeToStreamFrom(stream,
                                                               null,
                                                               false,
                                                               (_, x) => appeared.Signal(),
                                                               _ => Log.Info("Live processing started."),
                                                               (_, __, ___) => dropped.Signal());

                store.AppendToStream(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent());

                if (!appeared.Wait(Timeout))
                {
                    Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
                    Assert.Fail("Event appeared countdown event timed out.");
                }

                Assert.IsFalse(dropped.Wait(0));
                subscription.Stop(Timeout);
                Assert.IsTrue(dropped.Wait(Timeout));
            }
        }

        [Test, Category("LongRunning")]
        public void allow_multiple_subscriptions_to_same_stream()
        {
            const string stream = "allow_multiple_subscriptions_to_same_stream";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();
                var appeared = new CountdownEvent(2);
                var dropped1 = new ManualResetEventSlim(false);
                var dropped2 = new ManualResetEventSlim(false);

                var sub1 = store.SubscribeToStreamFrom(stream, 
                                                       null,
                                                       false,
                                                       (_, e) => appeared.Signal(),
                                                        _ => Log.Info("Live processing started."),
                                                       (x, y, z) => dropped1.Set());
                var sub2 = store.SubscribeToStreamFrom(stream,
                                                       null,
                                                       false,
                                                       (_, e) => appeared.Signal(),
                                                        _ => Log.Info("Live processing started."),
                                                       (x, y, z) => dropped2.Set());

                store.AppendToStream(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent());

                if (!appeared.Wait(Timeout))
                {
                    Assert.IsFalse(dropped1.Wait(0), "Subscription1 was dropped prematurely.");
                    Assert.IsFalse(dropped2.Wait(0), "Subscription2 was dropped prematurely.");
                    Assert.Fail("Couldn't wait for all events.");
                }

                Assert.IsFalse(dropped1.Wait(0));
                sub1.Stop(Timeout);
                Assert.IsTrue(dropped1.Wait(Timeout));

                Assert.IsFalse(dropped2.Wait(0));
                sub2.Stop(Timeout);
                Assert.IsTrue(dropped2.Wait(Timeout));
            }
        }

        [Test, Category("LongRunning")]
        public void call_dropped_callback_after_stop_method_call()
        {
            const string stream = "call_dropped_callback_after_stop_method_call";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();

                var dropped = new CountdownEvent(1);
                var subscription = store.SubscribeToStreamFrom(stream,
                                                               null,
                                                               false,
                                                               (x, y) => { },
                                                               _ => Log.Info("Live processing started."),
                                                               (x, y, z) => dropped.Signal());
                Assert.IsFalse(dropped.Wait(0));
                subscription.Stop(Timeout);
                Assert.IsTrue(dropped.Wait(Timeout));
            }
        }

        [Test, Category("LongRunning")]
        public void read_all_existing_events_and_keep_listening_to_new_ones()
        {
            const string stream = "read_all_existing_events_and_keep_listening_to_new_ones";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();

                var events = new List<ResolvedEvent>();
                var appeared = new CountdownEvent(20); // events
                var dropped = new CountdownEvent(1);

                for (int i = 0; i < 10; ++i)
                {
                    store.AppendToStream(stream, i-1, new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null));
                }

                var subscription = store.SubscribeToStreamFrom(stream,
                                                               null,
                                                               false,
                                                               (x, y) =>
                                                               {
                                                                   events.Add(y);
                                                                   appeared.Signal();
                                                               },
                                                               _ => Log.Info("Live processing started."),
                                                               (x, y, z) => dropped.Signal());
                for (int i = 10; i < 20; ++i)
                {
                    store.AppendToStream(stream, i-1, new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null));
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
            const string stream = "filter_events_and_keep_listening_to_new_ones";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();

                var events = new List<ResolvedEvent>();
                var appeared = new CountdownEvent(20); // skip first 10 events
                var dropped = new CountdownEvent(1);

                for (int i = 0; i < 20; ++i)
                {
                    store.AppendToStream(stream, i-1, new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null));
                }

                var subscription = store.SubscribeToStreamFrom(stream,
                                                               9,
                                                               false,
                                                               (x, y) =>
                                                               {
                                                                   events.Add(y);
                                                                   appeared.Signal();
                                                               },
                                                               _ => Log.Info("Live processing started."),
                                                               (x, y, z) => dropped.Signal());
                for (int i = 20; i < 30; ++i)
                {
                    store.AppendToStream(stream, i-1, new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null));
                }

                if (!appeared.Wait(Timeout))
                {
                    Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
                    Assert.Fail("Couldn't wait for all events.");
                }

                Assert.AreEqual(20, events.Count);
                for (int i = 0; i < 20; ++i)
                {
                    Assert.AreEqual("et-" + (i + 10).ToString(), events[i].OriginalEvent.EventType);
                }

                Assert.IsFalse(dropped.Wait(0));
                subscription.Stop(Timeout);
                Assert.IsTrue(dropped.Wait(Timeout));

                Assert.AreEqual(events.Last().OriginalEventNumber, subscription.LastProcessedEventNumber);
            }
        }

        [Test, Category("LongRunning")]
        public void filter_events_and_work_if_nothing_was_written_after_subscription()
        {
            const string stream = "filter_events_and_work_if_nothing_was_written_after_subscription";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();

                var events = new List<ResolvedEvent>();
                var appeared = new CountdownEvent(10);
                var dropped = new CountdownEvent(1);

                for (int i = 0; i < 20; ++i)
                {
                    store.AppendToStream(stream, i-1, new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null));
                }

                var subscription = store.SubscribeToStreamFrom(stream,
                                                               9,
                                                               false,
                                                               (x, y) =>
                                                               {
                                                                   events.Add(y);
                                                                   appeared.Signal();
                                                               },
                                                               _ => Log.Info("Live processing started."),
                                                               (x, y, z) => dropped.Signal());
                if (!appeared.Wait(Timeout))
                {
                    Assert.IsFalse(dropped.Wait(0), "Subscription was dropped prematurely.");
                    Assert.Fail("Couldn't wait for all events.");
                }

                Assert.AreEqual(10, events.Count);
                for (int i = 0; i < 10; ++i)
                {
                    Assert.AreEqual("et-" + (i + 10).ToString(), events[i].OriginalEvent.EventType);
                }

                Assert.IsFalse(dropped.Wait(0));
                subscription.Stop(Timeout);
                Assert.IsTrue(dropped.Wait(Timeout));

                Assert.AreEqual(events.Last().OriginalEventNumber, subscription.LastProcessedEventNumber);
            }
        }
    }
}
