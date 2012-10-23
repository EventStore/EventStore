using System.Collections.Generic;
using System.Linq;
using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.AllEvents
{
    [TestFixture]
    internal class read_all_events_forward_should
    {
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
        public void return_empty_slice_if_asked_to_read_from_end()
        {
            const string stream = "read_all_events_forward_should_return_empty_slice_if_asked_to_read_from_end";
            using (var store = new EventStoreConnection(Node.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
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
            using (var store = new EventStoreConnection(Node.TcpEndPoint))
            {
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
            using (var store = new EventStoreConnection(Node.TcpEndPoint))
            {
                var testEvents = Enumerable.Range(0, 5).Select(x => new TestEvent((x + 1).ToString())).ToArray();

                var create1 = store.CreateStreamAsync(stream + 1, new byte[0]);
                Assert.DoesNotThrow(create1.Wait);

                var write5to1 = store.AppendToStreamAsync(stream + 1, ExpectedVersion.EmptyStream, testEvents);
                Assert.DoesNotThrow(write5to1.Wait);

                var create2 = store.CreateStreamAsync(stream + 2, new byte[0]);
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
            using (var store = new EventStoreConnection(Node.TcpEndPoint))
            {
                var create1 = store.CreateStreamAsync(stream + 1, new byte[0]);
                Assert.DoesNotThrow(create1.Wait);

                var create2 = store.CreateStreamAsync(stream + 2, new byte[0]);
                Assert.DoesNotThrow(create2.Wait);

                var read = store.ReadAllEventsForwardAsync(Position.Start, 2);
                Assert.DoesNotThrow(read.Wait);

                Assert.That(read.Result.Events.Length, Is.EqualTo(2));
                Assert.That(read.Result.Events.All(x => x.EventType == "StreamCreated"));
            }
        }

        [Test]
        public void be_able_to_read_all_one_by_one_and_return_empty_slice_at_last()
        {
            const string stream = "read_all_events_forward_should_be_able_to_read_all_one_by_one_and_return_empty_slice_at_last";
            using (var store = new EventStoreConnection(Node.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
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
            using (var store = new EventStoreConnection(Node.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
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
            using (var store = new EventStoreConnection(Node.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
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
            using (var store = new EventStoreConnection(Node.TcpEndPoint))
            {
                var create1 = store.CreateStreamAsync(stream + 1, new byte[0]);
                Assert.DoesNotThrow(create1.Wait);

                var create2 = store.CreateStreamAsync(stream + 2, new byte[0]);
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
            using (var store = new EventStoreConnection(Node.TcpEndPoint))
            {
                var create1 = store.CreateStreamAsync(stream + 1, new byte[0]);
                Assert.DoesNotThrow(create1.Wait);

                var create2 = store.CreateStreamAsync(stream + 2, new byte[0]);
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
            using (var store = new EventStoreConnection(Node.TcpEndPoint))
            {
                var create1 = store.CreateStreamAsync(stream + 1, new byte[0]);
                Assert.DoesNotThrow(create1.Wait);

                var create2 = store.CreateStreamAsync(stream + 2, new byte[0]);
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
    }
}
