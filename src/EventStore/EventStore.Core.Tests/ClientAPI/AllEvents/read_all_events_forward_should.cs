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
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.AllEvents
{
    [TestFixture]
    internal class read_all_events_forward_should
    {
        private const int Timeout = 1000;

        private MiniNode _node;

        [SetUp]
        public void SetUp()
        {
            _node = new MiniNode();
            _node.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _node.Shutdown();
        }

        [Test]
        public void return_empty_slice_if_asked_to_read_from_end()
        {
            const string stream = "read_all_events_forward_should_return_empty_slice_if_asked_to_read_from_end";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, false, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 5).Select(x => new TestEvent((x + 1).ToString())).ToArray();

                var write5 = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write5.Wait);

                var read = store.ReadAllEventsForwardAsync(Position.End, 1);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(0));
            }
        }

        [Test]
        public void return_empty_slice_if_no_events_present()
        {
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var all = new List<RecordedEvent>();
                var position = Position.Start;
                AllEventsSlice slice;

                while ((slice = store.ReadAllEventsForward(position, 1)).Events.Any())
                {
                    all.Add(slice.Events.Single());
                    position = slice.Position;
                }

                Assert.That(all, Is.Empty);
            }
        }

        [Test]
        public void return_events_in_same_order_as_written()
        {
            const string stream = "read_all_events_forward_should_return_events_in_same_order_as_written";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var testEvents = Enumerable.Range(0, 5).Select(x => new TestEvent((x + 1).ToString())).ToArray();

                var create1 = store.CreateStreamAsync(stream + 1, false, new byte[0]);
                Assert.DoesNotThrow(create1.Wait);

                var write5to1 = store.AppendToStreamAsync(stream + 1, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write5to1.Wait);

                var create2 = store.CreateStreamAsync(stream + 2, false, new byte[0]);
                Assert.DoesNotThrow(create2.Wait);

                var write5to2 = store.AppendToStreamAsync(stream + 2, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write5to2.Wait);

                var read = store.ReadAllEventsForwardAsync(Position.Start, testEvents.Length*2 + 2);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(TestEventsComparer.Equal(testEvents.Concat(testEvents).ToArray(),
                                                     read.Result.Events.Skip(1).Take(testEvents.Length).Concat(read.Result.Events.Skip(testEvents.Length + 2).Take(testEvents.Length)).ToArray()));
            }
        }

        [Test]
        public void read_stream_created_events_as_well()
        {
            const string stream = "read_all_events_forward_should_read_system_events_as_well";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create1 = store.CreateStreamAsync(stream + 1, false, new byte[0]);
                Assert.DoesNotThrow(create1.Wait);

                var create2 = store.CreateStreamAsync(stream + 2, false, new byte[0]);
                Assert.DoesNotThrow(create2.Wait);

                var read = store.ReadAllEventsForwardAsync(Position.Start, 2);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(2));
                Assert.That(read.Result.Events.All(x => x.EventType == SystemEventTypes.StreamCreated));
            }
        }

        [Test]
        public void be_able_to_read_all_one_by_one_and_return_empty_slice_at_last()
        {
            const string stream = "read_all_events_forward_should_be_able_to_read_all_one_by_one_and_return_empty_slice_at_last";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, false, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 5).Select(x => new TestEvent((x + 1).ToString())).ToArray();

                var write = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write.Wait);

                var all = new List<RecordedEvent>();
                var position = Position.Start;
                AllEventsSlice slice;

                while ((slice = store.ReadAllEventsForward(position, 1)).Events.Any())
                {
                    all.Add(slice.Events.Single());
                    position = slice.Position;
                }

                Assert.That(TestEventsComparer.Equal(testEvents, all.Skip(1).ToArray()));
            }
        }

        [Test]
        public void be_able_to_read_events_slice_at_time()
        {
            const string stream = "read_all_events_forward_should_be_able_to_read_events_slice_at_time";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, false, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 20).Select(x => new TestEvent((x + 1).ToString())).ToArray();

                var write = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write.Wait);

                var all = new List<RecordedEvent>();
                var position = Position.Start;
                AllEventsSlice slice;

                while ((slice = store.ReadAllEventsForward(position, 5)).Events.Any())
                {
                    all.AddRange(slice.Events);
                    position = slice.Position;
                }

                Assert.That(TestEventsComparer.Equal(testEvents, all.Skip(1).ToArray()));
            }
        }

        [Test]
        public void return_partial_slice_if_not_enough_events()
        {
            const string stream = "read_all_events_forward_should_return_partial_slice_if_not_enough_events";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, false, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 20).Select(x => new TestEvent((x + 1).ToString())).ToArray();

                var write = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write.Wait);

                var read = store.ReadAllEventsForwardAsync(Position.Start, 25);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(testEvents.Length + 1));
            }
        }

        [Test]
        public void not_return_events_from_deleted_streams()
        {
            const string stream = "read_all_events_forward_should_not_return_events_from_deleted_streams";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create1 = store.CreateStreamAsync(stream + 1, false, new byte[0]);
                Assert.DoesNotThrow(create1.Wait);

                var create2 = store.CreateStreamAsync(stream + 2, false, new byte[0]);
                Assert.DoesNotThrow(create2.Wait);

                var testEvents = Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())).ToArray();

                var write1 = store.AppendToStreamAsync(stream + 1, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write1.Wait);

                var write2 = store.AppendToStreamAsync(stream + 2, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write2.Wait);

                var delete2 = store.DeleteStreamAsync(stream + 2, testEvents.Length);
                Assert.DoesNotThrow(delete2.Wait);

                var all = new List<RecordedEvent>();
                var position = Position.Start;
                AllEventsSlice slice;

                while ((slice = store.ReadAllEventsForward(position, 2)).Events.Any())
                {
                    all.AddRange(slice.Events);
                    position = slice.Position;
                }

                Assert.Inconclusive();
                //Assert.That(TestEventsComparer.Equal(testEvents, all.Skip(1).ToArray()));
            }
        }

        [Test]
        public void not_return_stream_deleted_records()
        {
            const string stream = "read_all_events_forward_should_not_return_stream_deleted_records";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create1 = store.CreateStreamAsync(stream + 1, false, new byte[0]);
                Assert.DoesNotThrow(create1.Wait);

                var create2 = store.CreateStreamAsync(stream + 2, false, new byte[0]);
                Assert.DoesNotThrow(create2.Wait);

                var delete1 = store.DeleteStreamAsync(stream + 1, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete1.Wait);

                var read = store.ReadAllEventsForwardAsync(Position.Start, 3);
                Assert.DoesNotThrow(read.Wait);

                Assert.Inconclusive();
                //Assert.That(read.Result.Events.Length, Is.EqualTo(1));
            }
        }

        [Test]
        public void return_no_records_if_stream_created_than_deleted()
        {
            const string stream = "read_all_events_forward_should_return_no_records_if_stream_created_than_deleted";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create1 = store.CreateStreamAsync(stream + 1, false, new byte[0]);
                Assert.DoesNotThrow(create1.Wait);

                var create2 = store.CreateStreamAsync(stream + 2, false, new byte[0]);
                Assert.DoesNotThrow(create2.Wait);

                var delete1 = store.DeleteStreamAsync(stream + 1, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete1.Wait);

                var delete2 = store.DeleteStreamAsync(stream + 2, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete2.Wait);

                var read = store.ReadAllEventsForwardAsync(Position.Start, 4);
                Assert.DoesNotThrow(read.Wait);

                Assert.Inconclusive();
                //Assert.That(read.Result.Events.Length, Is.EqualTo(0));
            }
        }

        [Test]
        public void recover_from_dropped_subscription_state_using_last_known_position()
        {
            const string stream = "read_all_events_forward_should_recover_from_dropped_subscription_state_using_last_known_position";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var catched = new List<RecordedEvent>();
                Position lastKnonwPosition = null;
                var dropped = new AutoResetEvent(false);

                var create = store.CreateStreamAsync(stream, false, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                store.SubscribeAsync(stream, 
                                     (@event, position) =>
                                     {
                                         catched.Add(@event);
                                         lastKnonwPosition = position;
                                     },
                                     () => dropped.Set());


                var testEvents = Enumerable.Range(1, 5).Select(x => new TestEvent(x.ToString())).ToArray();

                var write = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.That(write.Wait(Timeout));

                store.UnsubscribeAsync(stream);
                Assert.That(dropped.WaitOne(Timeout));

                var write2 = store.AppendToStreamAsync(stream, testEvents.Length, testEvents);
                Assert.That(write2.Wait(Timeout));

                var missed = store.ReadAllEventsForwardAsync(lastKnonwPosition, int.MaxValue);
                Assert.That(missed.Wait(Timeout));

                var expected = testEvents.Concat(testEvents).ToArray();
                var actual = catched.Concat(missed.Result.Events.Skip(1)).ToArray();//skip 1 because readallforward is inclusive
                Assert.That(TestEventsComparer.Equal(expected, actual));
            }
        }
    }
}
