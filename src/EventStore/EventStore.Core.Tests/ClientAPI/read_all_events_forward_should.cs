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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helper;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class read_all_events_forward_should: SpecificationWithDirectory
    {
        private const int Timeout = 10000;

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
        public void return_empty_slice_if_asked_to_read_from_end()
        {
            const string stream = "read_all_events_forward_should_return_empty_slice_if_asked_to_read_from_end";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();

                var testEvents = Enumerable.Range(0, 5).Select(x => TestEvent.NewTestEvent((x + 1).ToString())).ToArray();

                var write5 = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write5.Wait);

                var read = store.ReadAllEventsForwardAsync(Position.End, 1, false);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(0));
            }
        }

        [Test, Category("LongRunning")]
        public void return_empty_slice_if_no_events_present()
        {
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();
                var all = new List<RecordedEvent>();
                var position = Position.Start;
                AllEventsSlice slice;

                while ((slice = store.ReadAllEventsForward(position, 1, false)).Events.Any())
                {
                    all.Add(slice.Events.Single().Event);
                    position = slice.NextPosition;
                }

                Assert.That(all, Is.Empty);
            }
        }

        [Test, Category("LongRunning")]
        public void return_events_in_same_order_as_written()
        {
            const string stream = "read_all_events_forward_should_return_events_in_same_order_as_written";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();
                var testEvents = Enumerable.Range(0, 5).Select(x => TestEvent.NewTestEvent((x + 1).ToString())).ToArray();

                var write5to1 = store.AppendToStreamAsync(stream + 1, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write5to1.Wait);

                var write5to2 = store.AppendToStreamAsync(stream + 2, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write5to2.Wait);

                var read = store.ReadAllEventsForwardAsync(Position.Start, testEvents.Length*2 + 2, false);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(EventDataComparer.Equal(
                    testEvents.Concat(testEvents).ToArray(),
                    read.Result.Events.Select(x => x.Event).ToArray()));
            }
        }

        [Test, Category("LongRunning")]
        public void be_able_to_read_all_one_by_one_and_return_empty_slice_at_last()
        {
            const string stream = "read_all_events_forward_should_be_able_to_read_all_one_by_one_and_return_empty_slice_at_last";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();

                var testEvents = Enumerable.Range(0, 5).Select(x => TestEvent.NewTestEvent((x + 1).ToString())).ToArray();

                var write = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write.Wait);

                var all = new List<RecordedEvent>();
                var position = Position.Start;
                AllEventsSlice slice;

                while ((slice = store.ReadAllEventsForward(position, 1, false)).Events.Any())
                {
                    all.Add(slice.Events.Single().Event);
                    position = slice.NextPosition;
                }

                Assert.That(EventDataComparer.Equal(testEvents, all.ToArray()));
            }
        }

        [Test, Category("LongRunning")]
        public void be_able_to_read_events_slice_at_time()
        {
            const string stream = "read_all_events_forward_should_be_able_to_read_events_slice_at_time";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();

                var testEvents = Enumerable.Range(0, 20).Select(x => TestEvent.NewTestEvent((x + 1).ToString())).ToArray();

                var write = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write.Wait);

                var all = new List<RecordedEvent>();
                var position = Position.Start;
                AllEventsSlice slice;

                while ((slice = store.ReadAllEventsForward(position, 5, false)).Events.Any())
                {
                    all.AddRange(slice.Events.Select(x => x.Event));
                    position = slice.NextPosition;
                }

                Assert.That(EventDataComparer.Equal(testEvents, all.ToArray()));
            }
        }

        [Test, Category("LongRunning")]
        public void return_partial_slice_if_not_enough_events()
        {
            const string stream = "read_all_events_forward_should_return_partial_slice_if_not_enough_events";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();

                var testEvents = Enumerable.Range(0, 20).Select(x => TestEvent.NewTestEvent((x + 1).ToString())).ToArray();

                var write = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write.Wait);

                var read = store.ReadAllEventsForwardAsync(Position.Start, 25, false);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(testEvents.Length));
            }
        }

        [Test, Category("LongRunning")]
        public void not_return_events_from_deleted_streams()
        {
            Assert.Inconclusive();

            const string stream = "read_all_events_forward_should_not_return_events_from_deleted_streams";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();

                var testEvents = Enumerable.Range(0, 10).Select(x => TestEvent.NewTestEvent((x + 1).ToString())).ToArray();

                var write1 = store.AppendToStreamAsync(stream + 1, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write1.Wait);

                var write2 = store.AppendToStreamAsync(stream + 2, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write2.Wait);

                var delete2 = store.DeleteStreamAsync(stream + 2, testEvents.Length);
                Assert.DoesNotThrow(delete2.Wait);

                var all = new List<RecordedEvent>();
                var position = Position.Start;
                AllEventsSlice slice;

                while ((slice = store.ReadAllEventsForward(position, 2, false)).Events.Any())
                {
                    all.AddRange(slice.Events.Select(x => x.Event));
                    position = slice.NextPosition;
                }

                Assert.That(EventDataComparer.Equal(testEvents, all.Skip(1).ToArray()));
            }
        }

        [Test, Category("LongRunning")]
        public void return_stream_deleted_records()
        {
            const string stream = "read_all_events_forward_should_return_stream_deleted_records";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();

                store.DeleteStream(stream + 1, ExpectedVersion.EmptyStream);

                var res = store.ReadAllEventsForward(Position.Start, 3, false);
                Assert.That(res.Events.Length, Is.EqualTo(1));
            }
        }

        [Test, Category("LongRunning")]
        public void return_no_records_if_stream_created_than_deleted()
        {
            Assert.Inconclusive();

            const string stream = "read_all_events_forward_should_return_no_records_if_stream_created_than_deleted";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();

                var delete1 = store.DeleteStreamAsync(stream + 1, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete1.Wait);

                var delete2 = store.DeleteStreamAsync(stream + 2, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete2.Wait);

                var read = store.ReadAllEventsForwardAsync(Position.Start, 4, false);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(0));
            }
        }

        [Test, Category("LongRunning")]
        public void recover_from_dropped_subscription_state_using_last_known_position()
        {
            const string stream = "read_all_events_forward_should_recover_from_dropped_subscription_state_using_last_known_position";
            using (var store = TestConnection.Create(_node.TcpEndPoint))
            {
                store.Connect();
                
                var catched = new List<RecordedEvent>();
                Position? lastKnownPosition = null;
                var dropped = new AutoResetEvent(false);

                var subscribed = new ManualResetEventSlim();
                bool wasSubscribed = false;
                using (var subscription = store.SubscribeToStream(stream,
                                                                  false,
                                                                  (s, @event) =>
                                                                  {
                                                                      catched.Add(@event.Event);
                                                                      lastKnownPosition = @event.OriginalPosition;
                                                                      wasSubscribed = true;
                                                                      subscribed.Set();
                                                                  },
                                                                  (s, r, e) =>
                                                                  {
                                                                      wasSubscribed = false;
                                                                      subscribed.Set();
                                                                      dropped.Set();
                                                                  }).Result)
                {
                    var testEvents = Enumerable.Range(0, 5).Select(x => TestEvent.NewTestEvent(x.ToString())).ToArray();
                    store.AppendToStream(stream, ExpectedVersion.EmptyStream, testEvents);

                    Assert.IsTrue(subscribed.Wait(5000), "Subscription haven't happened in time.");
                    Assert.IsTrue(wasSubscribed, "Subscription failed.");
                    Assert.IsTrue(lastKnownPosition.HasValue, "Last know position should not be null.");

                    subscription.Unsubscribe();
                    Assert.That(dropped.WaitOne(Timeout), "Couldn't unsubscribe in time.");

                    store.AppendToStream(stream, testEvents.Length - 1, testEvents);
                    var missed = store.ReadAllEventsForwardAsync(lastKnownPosition.Value, int.MaxValue, false);
                    var expected = testEvents.Concat(testEvents).ToArray();
                    //skip 1 because ReadAllEventsForward is inclusive
                    var actual = catched.Concat(missed.Result.Events.Skip(1).Select(x => x.Event)).ToArray();
                    Assert.That(EventDataComparer.Equal(expected, actual));
                }
            }
        }
    }
}
