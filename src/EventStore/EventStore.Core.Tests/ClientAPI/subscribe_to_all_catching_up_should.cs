using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Common.Log;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
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
        private IEventStoreConnection _conn;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            _node = new MiniNode(PathName, skipInitializeStandardUsersCheck: false);
            _node.Start();

            _conn = TestConnection.Create(_node.TcpEndPoint);
            _conn.ConnectAsync().Wait();
            _conn.SetStreamMetadataAsync("$all", -1,
                                    StreamMetadata.Build().SetReadRole(SystemRoles.All),
                                    new UserCredentials(SystemUsers.Admin, SystemUsers.DefaultAdminPassword)).Wait();
        }

        [TearDown]
        public override void TearDown()
        {
            _conn.Close();
            _node.Shutdown();
            base.TearDown();
        }
        
        [Test, Category("LongRunning")]
        public void call_dropped_callback_after_stop_method_call()
        {
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.ConnectAsync().Wait();

                var dropped = new CountdownEvent(1);
                var subscription = store.SubscribeToAllFrom(null,
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
        public void be_able_to_subscribe_to_empty_db()
        {
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.ConnectAsync().Wait();
                var appeared = new ManualResetEventSlim(false);
                var dropped = new CountdownEvent(1);

                var subscription = store.SubscribeToAllFrom(null,
                                                            false,
                                                            (_, x) =>
                                                            {
                                                                if (!SystemStreams.IsSystemStream(x.OriginalEvent.EventStreamId))
                                                                    appeared.Set();
                                                            },
                                                            _ => Log.Info("Live processing started."),
                                                            (_, __, ___) => dropped.Signal());

                Thread.Sleep(100); // give time for first pull phase
                store.SubscribeToAll(false, (s, x) => { }, (s, r, e) => { });
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
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.ConnectAsync().Wait();

                var events = new List<ResolvedEvent>();
                var appeared = new CountdownEvent(20);
                var dropped = new CountdownEvent(1);

                for (int i = 0; i < 10; ++i)
                {
                    store.AppendToStreamAsync("stream-" + i.ToString(), -1, new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null)).Wait();
                }

                var subscription = store.SubscribeToAllFrom(null,
                                                            false,
                                                            (x, y) =>
                                                            {
                                                                if (!SystemStreams.IsSystemStream(y.OriginalEvent.EventStreamId))
                                                                {
                                                                    events.Add(y);
                                                                    appeared.Signal();
                                                                }
                                                            },
                                                            _ => Log.Info("Live processing started."),
                                                            (x, y, z) => dropped.Signal());
                for (int i = 10; i < 20; ++i)
                {
                    store.AppendToStreamAsync("stream-" + i.ToString(), -1, new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null)).Wait();
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
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.ConnectAsync().Wait();

                var events = new List<ResolvedEvent>();
                var appeared = new CountdownEvent(10);
                var dropped = new CountdownEvent(1);

                for (int i = 0; i < 10; ++i)
                {
                    store.AppendToStream("stream-" + i.ToString(), -1, new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null));
                }

                var allSlice = store.ReadAllEventsForwardAsync(Position.Start, 100, false).Result;
                var lastEvent = allSlice.Events.Last();

                var subscription = store.SubscribeToAllFrom(lastEvent.OriginalPosition,
                                                            false,
                                                            (x, y) =>
                                                            {
                                                                events.Add(y);
                                                                appeared.Signal();
                                                            },
                                                            _ => Log.Info("Live processing started."),
                                                            (x, y, z) =>
                                                            {
                                                                Log.Info("Subscription dropped: {0}, {1}.", y, z);
                                                                dropped.Signal();
                                                            });

                for (int i = 10; i < 20; ++i)
                {
                    store.AppendToStreamAsync("stream-" + i.ToString(), -1, new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null)).Wait();
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
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();

                var events = new List<ResolvedEvent>();
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                for (int i = 0; i < 10; ++i)
                {
                    store.AppendToStreamAsync("stream-" + i.ToString(), -1, new EventData(Guid.NewGuid(), "et-" + i.ToString(), false, new byte[3], null)).Wait();
                }

                var allSlice = store.ReadAllEventsForwardAsync(Position.Start, 100, false).Result;
                var lastEvent = allSlice.Events[allSlice.Events.Length - 2];

                var subscription = store.SubscribeToAllFrom(lastEvent.OriginalPosition,
                                                            false,
                                                            (x, y) =>
                                                            {
                                                                events.Add(y);
                                                                appeared.Signal();
                                                            },
                                                            _ => Log.Info("Live processing started."),
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
