using System;
using System.Linq;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture]
    internal class read_event_stream_backward_should
    {
        [Test]
        public void throw_if_count_le_zero()
        {
            const string stream = "read_event_stream_backward_should_throw_if_count_le_zero";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => store.ReadEventStreamBackwardAsync(stream, 0, 0));
            }
        }

        [Test]
        public void throw_if_no_stream()
        {
            const string stream = "read_event_stream_backward_should_throw_if_no_stream";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var read = store.ReadEventStreamBackwardAsync(stream, StreamPosition.End, 1);
                Assert.That(() => read.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDoesNotExistException>());
            }
        }

        [Test]
        public void throw_if_stream_deleted()
        {
            const string stream = "read_event_stream_backward_should_throw_if_stream_deleted";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);
                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete.Wait);

                var read = store.ReadEventStreamBackwardAsync(stream, StreamPosition.End, 1);
                Assert.That(() => read.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test]
        public void return_single_event_when_called_on_empty_stream()
        {
            const string stream = "read_event_stream_backward_should_return_single_event_when_called_on_empty_stream";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var read = store.ReadEventStreamBackwardAsync(stream, StreamPosition.End, 1);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(1));
            }
        }

        [Test]
        public void return_partial_slice_if_no_enough_events_in_stream()
        {
            const string stream = "read_event_stream_backward_should_return_partial_slice_if_no_enough_events_in_stream";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())).ToArray();
                var write10 = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write10.Wait);

                var read = store.ReadEventStreamBackwardAsync(stream, 1, 5);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(2));
            }
        }

        [Test]
        public void return_events_reversed_compared_to_written()
        {
            const string stream = "read_event_stream_backward_should_return_events_reversed_compared_to_written";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())).ToArray();
                var write10 = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write10.Wait);

                var read = store.ReadEventStreamBackwardAsync(stream, StreamPosition.End, testEvents.Length);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(TestEventsComparer.Equal(testEvents.Reverse().ToArray(), read.Result.Events));
            }
        }

        [Test]
        public void be_able_to_read_single_event_from_arbitrary_position()
        {
            const string stream = "read_event_stream_backward_should_be_able_to_read_single_event_from_arbitrary_position";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())).ToArray();
                var write10 = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write10.Wait);

                var read = store.ReadEventStreamBackwardAsync(stream, 7, 1);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(TestEventsComparer.Equal(testEvents[6], read.Result.Events.Single()));
            }
        }

        [Test]
        public void be_able_to_read_first_event()
        {
            const string stream = "read_event_stream_backward_should_be_able_to_read_first_event";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())).ToArray();
                var write10 = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write10.Wait);

                var read = store.ReadEventStreamBackwardAsync(stream, StreamPosition.Start, 1);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(1));
            }
        }

        [Test]
        public void be_able_to_read_last_event()
        {
            const string stream = "read_event_stream_backward_should_be_able_to_read_last_event";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())).ToArray();
                var write10 = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write10.Wait);

                var read = store.ReadEventStreamBackwardAsync(stream, StreamPosition.End, 1);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(TestEventsComparer.Equal(testEvents.Last(), read.Result.Events.Single()));
            }
        }

        [Test]
        public void be_able_to_read_slice_from_arbitrary_position()
        {
            const string stream = "read_event_stream_backward_should_be_able_to_read_slice_from_arbitrary_position";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var testEvents = Enumerable.Range(0, 10).Select(x => new TestEvent((x + 1).ToString())).ToArray();
                var write10 = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write10.Wait);

                var read = store.ReadEventStreamBackwardAsync(stream, 3, 2);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(TestEventsComparer.Equal(testEvents.Skip(1).Take(2).Reverse().ToArray(), read.Result.Events));
            }
        }
    }
}
