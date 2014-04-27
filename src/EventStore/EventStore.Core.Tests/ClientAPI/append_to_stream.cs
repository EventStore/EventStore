using System;
using System.Linq;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class append_to_stream : SpecificationWithDirectoryPerTestFixture
    {
        private readonly TcpType _tcpType = TcpType.Normal;
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

        [Test, Category("LongRunning"), Category("Network")]
        public void should_allow_appending_zero_events_to_stream_with_no_problems()
        {
            const string stream1 = "should_allow_appending_zero_events_to_stream_with_no_problems1";
            const string stream2 = "should_allow_appending_zero_events_to_stream_with_no_problems2";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();

                Assert.AreEqual(-1, store.AppendToStreamAsync(stream1, ExpectedVersion.Any).Result.NextExpectedVersion);
                Assert.AreEqual(-1, store.AppendToStreamAsync(stream1, ExpectedVersion.NoStream).Result.NextExpectedVersion);
                Assert.AreEqual(-1, store.AppendToStreamAsync(stream1, ExpectedVersion.Any).Result.NextExpectedVersion);
                Assert.AreEqual(-1, store.AppendToStreamAsync(stream1, ExpectedVersion.NoStream).Result.NextExpectedVersion);

                var read1 = store.ReadStreamEventsForwardAsync(stream1, 0, 2, resolveLinkTos: false).Result;
                Assert.That(read1.Events.Length, Is.EqualTo(0));

                Assert.AreEqual(-1, store.AppendToStreamAsync(stream2, ExpectedVersion.NoStream).Result.NextExpectedVersion);
                Assert.AreEqual(-1, store.AppendToStreamAsync(stream2, ExpectedVersion.Any).Result.NextExpectedVersion);
                Assert.AreEqual(-1, store.AppendToStreamAsync(stream2, ExpectedVersion.NoStream).Result.NextExpectedVersion);
                Assert.AreEqual(-1, store.AppendToStreamAsync(stream2, ExpectedVersion.Any).Result.NextExpectedVersion);

                var read2 = store.ReadStreamEventsForwardAsync(stream2, 0, 2, resolveLinkTos: false).Result;
                Assert.That(read2.Events.Length, Is.EqualTo(0));
            }
        }
        
        [Test, Category("LongRunning"), Category("Network")]
        public void should_create_stream_with_no_stream_exp_ver_on_first_write_if_does_not_exist()
        {
            const string stream = "should_create_stream_with_no_stream_exp_ver_on_first_write_if_does_not_exist";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();

                Assert.AreEqual(0, store.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent()).Result.NextExpectedVersion);

                var read = store.ReadStreamEventsForwardAsync(stream, 0, 2, resolveLinkTos: false);
                Assert.DoesNotThrow(read.Wait);
                Assert.That(read.Result.Events.Length, Is.EqualTo(1));
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_create_stream_with_any_exp_ver_on_first_write_if_does_not_exist()
        {
            const string stream = "should_create_stream_with_any_exp_ver_on_first_write_if_does_not_exist";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();
                Assert.AreEqual(0, store.AppendToStreamAsync(stream, ExpectedVersion.Any, TestEvent.NewTestEvent()).Result.NextExpectedVersion);

                var read = store.ReadStreamEventsForwardAsync(stream, 0, 2, resolveLinkTos: false);
                Assert.DoesNotThrow(read.Wait);
                Assert.That(read.Result.Events.Length, Is.EqualTo(1));
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_writing_with_correct_exp_ver_to_deleted_stream()
        {
            const string stream = "should_fail_writing_with_correct_exp_ver_to_deleted_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true);
                Assert.DoesNotThrow(delete.Wait);

                var append = store.AppendToStreamAsync(stream, ExpectedVersion.NoStream, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_writing_with_any_exp_ver_to_deleted_stream()
        {
            const string stream = "should_fail_writing_with_any_exp_ver_to_deleted_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();

                try
                {
                    store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true).Wait();
                }
                catch (Exception exc)
                {
                    Console.WriteLine(exc);
                    Assert.Fail();
                }

                var append = store.AppendToStreamAsync(stream, ExpectedVersion.Any, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_writing_with_invalid_exp_ver_to_deleted_stream()
        {
            const string stream = "should_fail_writing_with_invalid_exp_ver_to_deleted_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true);
                Assert.DoesNotThrow(delete.Wait);

                var append = store.AppendToStreamAsync(stream, 5, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_append_with_correct_exp_ver_to_existing_stream()
        {
            const string stream = "should_append_with_correct_exp_ver_to_existing_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();
                store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()).Wait();

                var append = store.AppendToStreamAsync(stream, 0, new[] { TestEvent.NewTestEvent() });
                Assert.DoesNotThrow(append.Wait);
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_append_with_any_exp_ver_to_existing_stream()
        {
            const string stream = "should_append_with_any_exp_ver_to_existing_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();
                Assert.AreEqual(0, store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()).Result.NextExpectedVersion);
                Assert.AreEqual(1, store.AppendToStreamAsync(stream, ExpectedVersion.Any, TestEvent.NewTestEvent()).Result.NextExpectedVersion);
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_appending_with_wrong_exp_ver_to_existing_stream()
        {
            const string stream = "should_fail_appending_with_wrong_exp_ver_to_existing_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();
 
                var append = store.AppendToStreamAsync(stream, 1, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void can_append_multiple_events_at_once()
        {
            const string stream = "can_append_multiple_events_at_once";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();

                var events = Enumerable.Range(0, 100).Select(i => TestEvent.NewTestEvent(i.ToString(), i.ToString()));
                Assert.AreEqual(99, store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, events).Result.NextExpectedVersion);
            }
        }
    }

    [TestFixture, Category("LongRunning")]
    public class ssl_append_to_stream : SpecificationWithDirectoryPerTestFixture
    {
        private readonly TcpType _tcpType = TcpType.Ssl;
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

        [Test, Category("LongRunning"), Category("Network")]
        public void should_allow_appending_zero_events_to_stream_with_no_problems()
        {
            const string stream = "should_allow_appending_zero_events_to_stream_with_no_problems";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();
                Assert.AreEqual(-1, store.AppendToStreamAsync(stream, ExpectedVersion.NoStream).Result.NextExpectedVersion);

                var read = store.ReadStreamEventsForwardAsync(stream, 0, 2, resolveLinkTos: false).Result;
                Assert.That(read.Events.Length, Is.EqualTo(0));
            }
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void should_create_stream_with_no_stream_exp_ver_on_first_write_if_does_not_exist()
        {
            const string stream = "should_create_stream_with_no_stream_exp_ver_on_first_write_if_does_not_exist";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();
                Assert.AreEqual(0, store.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent()).Result.NextExpectedVersion);

                var read = store.ReadStreamEventsForwardAsync(stream, 0, 2, resolveLinkTos: false);
                Assert.DoesNotThrow(read.Wait);
                Assert.That(read.Result.Events.Length, Is.EqualTo(1));
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_create_stream_with_any_exp_ver_on_first_write_if_does_not_exist()
        {
            const string stream = "should_create_stream_with_any_exp_ver_on_first_write_if_does_not_exist";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();

                var read = store.ReadStreamEventsForwardAsync(stream, 0, 2, resolveLinkTos: false);
                Assert.DoesNotThrow(read.Wait);
                Assert.That(read.Result.Events.Length, Is.EqualTo(1));
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_writing_with_correct_exp_ver_to_deleted_stream()
        {
            const string stream = "should_fail_writing_with_correct_exp_ver_to_deleted_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true);
                Assert.DoesNotThrow(delete.Wait);

                var append = store.AppendToStreamAsync(stream, ExpectedVersion.NoStream, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_writing_with_any_exp_ver_to_deleted_stream()
        {
            const string stream = "should_fail_writing_with_any_exp_ver_to_deleted_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true);
                Assert.DoesNotThrow(delete.Wait);

                var append = store.AppendToStreamAsync(stream, ExpectedVersion.Any, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_writing_with_invalid_exp_ver_to_deleted_stream()
        {
            const string stream = "should_fail_writing_with_invalid_exp_ver_to_deleted_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true);
                Assert.DoesNotThrow(delete.Wait);

                var append = store.AppendToStreamAsync(stream, 5, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_append_with_correct_exp_ver_to_existing_stream()
        {
            const string stream = "should_append_with_correct_exp_ver_to_existing_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();
                Assert.AreEqual(0, store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()).Result.NextExpectedVersion);
                Assert.AreEqual(1, store.AppendToStreamAsync(stream, 0, TestEvent.NewTestEvent()).Result.NextExpectedVersion);
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_append_with_any_exp_ver_to_existing_stream()
        {
            const string stream = "should_append_with_any_exp_ver_to_existing_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();
                Assert.AreEqual(0, store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()).Result.NextExpectedVersion);
                Assert.AreEqual(1, store.AppendToStreamAsync(stream, ExpectedVersion.Any, TestEvent.NewTestEvent()).Result.NextExpectedVersion);
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void should_fail_appending_with_wrong_exp_ver_to_existing_stream()
        {
            const string stream = "should_fail_appending_with_wrong_exp_ver_to_existing_stream";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();
                Assert.AreEqual(0, store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()).Result.NextExpectedVersion);

                var append = store.AppendToStreamAsync(stream, 1, new[] { TestEvent.NewTestEvent() });
                Assert.That(() => append.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test, Category("LongRunning")]
        [Category("Network")]
        public void can_append_multiple_events_at_once()
        {
            const string stream = "can_append_multiple_events_at_once";
            using (var store = TestConnection.To(_node, _tcpType))
            {
                store.ConnectAsync().Wait();

                var events = Enumerable.Range(0, 100).Select(i => TestEvent.NewTestEvent(i.ToString(), i.ToString()));
                Assert.AreEqual(99, store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, events).Result.NextExpectedVersion);
            }
        }
    }

}
