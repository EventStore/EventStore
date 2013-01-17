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
using EventStore.ClientAPI;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.AllEvents
{
    [TestFixture, Category("LongRunning")]
    public class read_all_events_backward_should: SpecificationWithDirectory
    {
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
        public void return_empty_slice_if_asked_to_read_from_start()
        {
            const string stream = "read_all_events_backward_should_return_empty_slice_if_asked_to_read_from_start";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var read = store.ReadAllEventsBackwardAsync(Position.Start, 1, false);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(0));
            }
        }

        [Test, Category("LongRunning")]
        public void return_empty_slice_if_no_events_present()
        {
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var all = new List<RecordedEvent>();
                var position = Position.End;
                AllEventsSlice slice;

                while ((slice = store.ReadAllEventsBackward(position, 1, false)).Events.Any())
                {
                    all.Add(slice.Events.Single().Event);
                    position = slice.NextPosition;
                }

                Assert.That(all, Is.Empty);
            }
        }

        [Test, Category("LongRunning")]
        public void return_partial_slice_if_not_enough_events()
        {
            const string stream = "read_all_events_backward_should_return_partial_slice_if_not_enough_events";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 20).Select(x => new TestEvent((x + 1).ToString())).ToArray();

                var write = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write.Wait);

                var read = store.ReadAllEventsBackwardAsync(Position.End, 25, false);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(testEvents.Length + 1));
            }
        }

        [Test, Category("LongRunning")]
        public void return_events_in_reversed_order_compared_to_written()
        {
            const string stream = "read_all_events_backward_should_return_events_in_reversed_order_compared_to_written";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var testEvents = Enumerable.Range(0, 5).Select(x => new TestEvent((x + 1).ToString())).ToArray();

                var create = store.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var write = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write.Wait);

                var read = store.ReadAllEventsBackwardAsync(Position.End, testEvents.Length + 1, false);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(EventDataComparer.Equal(testEvents.Reverse().ToArray(), 
                                                     read.Result.Events.Select(x => x.Event).Take(testEvents.Length).ToArray()));
            }
        }

        [Test, Category("LongRunning")]
        public void read_stream_created_events_as_well()
        {
            const string stream = "read_all_events_backward_should_read_system_events_as_well";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create1 = store.CreateStreamAsync(stream + 1, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(create1.Wait);

                var create2 = store.CreateStreamAsync(stream + 2, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(create2.Wait);

                var read = store.ReadAllEventsBackwardAsync(Position.End, 2, false);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(2));
                Assert.That(read.Result.Events.All(x => x.Event.EventType == SystemEventTypes.StreamCreated));
            }
        }

        [Test, Category("LongRunning")]
        public void be_able_to_read_all_one_by_one_and_return_empty_slice_at_last()
        {
            const string stream = "read_all_events_backward_should_be_able_to_read_all_one_by_one_and_return_empty_slice_at_last";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 5).Select(x => new TestEvent((x + 1).ToString())).ToArray();

                var write = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write.Wait);

                var all = new List<RecordedEvent>();
                var position = Position.End;
                AllEventsSlice slice;

                while ((slice = store.ReadAllEventsBackward(position, 1, false)).Events.Any())
                {
                    all.Add(slice.Events.Single().Event);
                    position = slice.NextPosition;
                }

                Assert.That(EventDataComparer.Equal(testEvents.Reverse().ToArray(), all.Take(testEvents.Length).ToArray()));
            }
        }

        [Test, Category("LongRunning")]
        public void be_able_to_read_events_slice_at_time()
        {
            const string stream = "read_all_events_backward_should_be_able_to_read_events_slice_at_time";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())).ToArray();

                var write = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write.Wait);

                var all = new List<RecordedEvent>();
                var position = Position.End;
                AllEventsSlice slice;

                while ((slice = store.ReadAllEventsBackward(position, 5, false)).Events.Any())
                {
                    all.AddRange(slice.Events.Select(x => x.Event));
                    position = slice.NextPosition;
                }

                Assert.That(EventDataComparer.Equal(testEvents.Reverse().ToArray(), all.Take(testEvents.Length).ToArray()));
            }
        }

        [Test, Category("LongRunning")]
        public void not_return_events_from_deleted_streams()
        {
            const string stream = "read_all_events_backward_should_not_return_events_from_deleted_streams";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create1 = store.CreateStreamAsync(stream + 1, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(create1.Wait);

                var create2 = store.CreateStreamAsync(stream + 2, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(create2.Wait);

                var testEvents = Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())).ToArray();

                var write1 = store.AppendToStreamAsync(stream + 1, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write1.Wait);

                var write2 = store.AppendToStreamAsync(stream + 2, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write2.Wait);

                var delete2 = store.DeleteStreamAsync(stream + 2, testEvents.Length);
                Assert.DoesNotThrow(delete2.Wait);

                var all = new List<RecordedEvent>();
                var position = Position.End;
                AllEventsSlice slice;

                while ((slice = store.ReadAllEventsBackward(position, 2, false)).Events.Any())
                {
                    all.AddRange(slice.Events.Select(x => x.Event));
                    position = slice.NextPosition;
                }

                Assert.Inconclusive();
                //Assert.That(EventDataComparer.Equal(testEvents.Reverse().ToArray(), all.Take(testEvents.Length).ToArray()));
            }
        }

        [Test, Category("LongRunning")]
        public void not_return_stream_deleted_records()
        {
            Assert.Inconclusive();

            const string stream = "read_all_events_backward_should_not_return_stream_deleted_records";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create1 = store.CreateStreamAsync(stream + 1, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(create1.Wait);

                var create2 = store.CreateStreamAsync(stream + 2, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(create2.Wait);

                var delete1 = store.DeleteStreamAsync(stream + 1, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete1.Wait);

                var read = store.ReadAllEventsBackwardAsync(Position.End, 3, false);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(1));
            }
        }

        [Test, Category("LongRunning")]
        public void return_no_records_if_stream_created_than_deleted()
        {
            Assert.Inconclusive();

            const string stream = "read_all_events_backward_should_return_no_records_if_stream_created_than_deleted";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, Guid.NewGuid(), false, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete.Wait);

                var read = store.ReadAllEventsBackwardAsync(Position.Start, 2, false);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(0));
            }
        }
    }
}
