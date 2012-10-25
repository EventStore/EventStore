using System;
using System.Linq;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture]
    internal class append_to_stream
    {
        [Test]
        [Category("Network")]
        public void should_create_stream_with_no_stream_exp_ver_on_first_write_if_does_not_exist()
        {
            const string stream = "should_create_stream_with_no_stream_exp_ver_on_first_write_if_does_not_exist";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var append = store.AppendToStreamAsync(stream, ExpectedVersion.NoStream, new[] {new TestEvent()});
                Assert.DoesNotThrow(append.Wait);

                var read = store.ReadEventStreamForwardAsync(stream, 0, 2);
                Assert.DoesNotThrow(read.Wait);
                Assert.That(read.Result.Events.Length, Is.EqualTo(2));
            }
        }

        [Test]
        [Category("Network")]
        public void should_create_stream_with_any_exp_ver_on_first_write_if_does_not_exist()
        {
            const string stream = "should_create_stream_with_any_exp_ver_on_first_write_if_does_not_exist";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var append = store.AppendToStreamAsync(stream, ExpectedVersion.Any, new[] { new TestEvent() });
                Assert.DoesNotThrow(append.Wait);

                var read = store.ReadEventStreamForwardAsync(stream, 0, 2);
                Assert.DoesNotThrow(read.Wait);
                Assert.That(read.Result.Events.Length, Is.EqualTo(2));
            }
        }

        [Test]
        [Category("Network")]
        public void should_fail_to_create_stream_with_wrong_exp_ver_on_first_write_if_does_not_exist()
        {
            const string stream = "should_fail_to_create_stream_with_wrong_exp_ver_on_first_write_if_does_not_exist";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var append = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, new[] { new TestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test]
        [Category("Network")]
        public void should_fail_writing_with_correct_exp_ver_to_deleted_stream()
        {
            const string stream = "should_fail_writing_with_correct_exp_ver_to_deleted_stream";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete.Wait);

                var append = store.AppendToStreamAsync(stream, ExpectedVersion.NoStream, new[] { new TestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test]
        [Category("Network")]
        public void should_fail_writing_with_any_exp_ver_to_deleted_stream()
        {
            const string stream = "should_fail_writing_with_any_exp_ver_to_deleted_stream";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete.Wait);

                var append = store.AppendToStreamAsync(stream, ExpectedVersion.Any, new[] { new TestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test]
        [Category("Network")]
        public void should_fail_writing_with_invalid_exp_ver_to_deleted_stream()
        {
            const string stream = "should_fail_writing_with_invalid_exp_ver_to_deleted_stream";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete.Wait);

                var append = store.AppendToStreamAsync(stream, 5, new[] { new TestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test]
        [Category("Network")]
        public void should_append_with_correct_exp_ver_to_existing_stream()
        {
            const string stream = "should_append_with_correct_exp_ver_to_existing_stream";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var append = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, new[] { new TestEvent() });
                Assert.DoesNotThrow(append.Wait);
            }
        }

        [Test]
        [Category("Network")]
        public void should_append_with_any_exp_ver_to_existing_stream()
        {
            const string stream = "should_append_with_any_exp_ver_to_existing_stream";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var append = store.AppendToStreamAsync(stream, ExpectedVersion.Any, new[] { new TestEvent() });
                Assert.DoesNotThrow(append.Wait);
            }
        }

        [Test]
        [Category("Network")]
        public void should_fail_appending_with_wrong_exp_ver_to_existing_stream()
        {
            const string stream = "should_fail_appending_with_wrong_exp_ver_to_existing_stream";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var append = store.AppendToStreamAsync(stream, 1, new[] { new TestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test]
        [Category("Network")]
        public void can_append_multiple_events_at_once()
        {
            const string stream = "can_append_multiple_events_at_once";
            using (var store = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = store.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var events = Enumerable.Range(0, 100).Select(i => new TestEvent(i.ToString(), i.ToString()));
                var append = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, events);
                Assert.DoesNotThrow(append.Wait);
            }
        }
    }
}
