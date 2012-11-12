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
using System.Linq;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;
namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture]
    internal class read_event_stream_forward_should
    {
        [Test]
        [Category("Network")]
        public void throw_if_count_le_zero()
        {
            const string stream = "read_event_stream_forward_should_throw_if_count_le_zero";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                Assert.Throws<ArgumentOutOfRangeException>(() => store.ReadEventStreamForwardAsync(stream, 0, 0));
            }
        }

        [Test]
        [Category("Network")]
        public void throw_if_start_lt_zero()
        {
            const string stream = "read_event_stream_forward_should_throw_if_start_lt_zero";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                Assert.Throws<ArgumentOutOfRangeException>(() => store.ReadEventStreamForwardAsync(stream, -1, 1));
            }
        }

        [Test]
        [Category("Network")]
        public void throw_if_no_stream()
        {
            const string stream = "read_event_stream_forward_should_throw_if_no_stream";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var read = store.ReadEventStreamForwardAsync(stream, 0, 1);
                Assert.That(() => read.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDoesNotExistException>());
            }
        }

        [Test]
        [Category("Network")]
        public void throw_if_stream_deleted()
        {
            const string stream = "read_event_stream_forward_should_throw_if_stream_deleted";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);
                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete.Wait);

                var read = store.ReadEventStreamForwardAsync(stream, 0, 1);
                Assert.That(() => read.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test]
        [Category("Network")]
        public void return_single_event_when_called_on_empty_stream()
        {
            const string stream = "read_event_stream_forward_should_return_single_event_when_called_on_empty_stream";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var read = store.ReadEventStreamForwardAsync(stream, 0, 1);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(1));
            }
        }

        [Test]
        [Category("Network")]
        public void return_empty_slice_when_called_on_empty_stream_starting_at_position_1()
        {
            const string stream = "read_event_stream_forward_should_return_empty_slice_when_called_on_empty_stream_starting_at_position_1";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var read = store.ReadEventStreamForwardAsync(stream, 1, 1);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(0));
            }
        }

        [Test]
        [Category("Network")]
        public void return_empty_slice_when_called_on_non_existing_range()
        {
            const string stream = "read_event_stream_forward_should_return_empty_slice_when_called_on_non_existing_range";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var write10 = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())));
                Assert.DoesNotThrow(write10.Wait);

                var read = store.ReadEventStreamForwardAsync(stream, 11, 5);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(0));
            }
        }

        [Test]
        [Category("Network")]
        public void return_partial_slice_if_no_enough_events_in_stream()
        {
            const string stream = "read_event_stream_forward_should_return_partial_slice_if_no_enough_events_in_stream";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var write10 = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())));
                Assert.DoesNotThrow(write10.Wait);

                var read = store.ReadEventStreamForwardAsync(stream, 10, 5);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(1));
            }
        }

        [Test]
        [Category("Network")]
        public void return_partial_slice_when_got_int_max_value_as_maxcount()
        {
            const string stream = "read_event_stream_forward_should_return_partial_slice_when_got_int_max_value_as_maxcount";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var write10 = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())));
                Assert.DoesNotThrow(write10.Wait);

                var read = store.ReadEventStreamForwardAsync(stream, StreamPosition.FirstClientEvent, int.MaxValue);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(10));
            }
        }

        [Test]
        [Category("Network")]
        public void return_events_in_same_order_as_written()
        {
            const string stream = "read_event_stream_forward_should_return_events_in_same_order_as_written";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())).ToArray();
                var write10 = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write10.Wait);

                var read = store.ReadEventStreamForwardAsync(stream, StreamPosition.FirstClientEvent, testEvents.Length);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(TestEventsComparer.Equal(testEvents, read.Result.Events));
            }
        }

        [Test]
        [Category("Network")]
        public void be_able_to_read_single_event_from_arbitrary_position()
        {
            const string stream = "read_event_stream_forward_should_be_able_to_read_from_arbitrary_position";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())).ToArray();
                var write10 = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write10.Wait);

                var read = store.ReadEventStreamForwardAsync(stream, 5, 1);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(TestEventsComparer.Equal(testEvents[4], read.Result.Events.Single()));
            }
        }

        [Test]
        [Category("Network")]
        public void be_able_to_read_slice_from_arbitrary_position()
        {
            const string stream = "read_event_stream_forward_should_be_able_to_read_slice_from_arbitrary_position";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())).ToArray();
                var write10 = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write10.Wait);

                var read = store.ReadEventStreamForwardAsync(stream, 5, 2);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(TestEventsComparer.Equal(testEvents.Skip(4).Take(2).ToArray(), read.Result.Events));
            }
        }
    }
}
