using System;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture]
    internal class deleting_stream
    {
        [Test]
        public void which_already_exists_should_success_when_passed_empty_stream_expected_version()
        {
            const string stream = "which_already_exists_should_success_when_passed_empty_stream_expected_version";
            using (var connection = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = connection.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var delete = connection.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete.Wait);
            }
        }

        [Test]
        public void which_already_exists_should_success_when_passed_any_for_expected_version()
        {
            const string stream = "which_already_exists_should_success_when_passed_any_for_expected_version";
            using (var connection = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = connection.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var delete = connection.DeleteStreamAsync(stream, ExpectedVersion.Any);
                Assert.DoesNotThrow(delete.Wait);
            }
        }

        [Test]
        public void with_invalid_expected_version_should_fail()
        {
            const string stream = "with_invalid_expected_version_should_fail";
            using (var connection = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = connection.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var delete = connection.DeleteStreamAsync(stream, 1);
                Assert.That(() => delete.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test]
        public void which_does_not_exist_should_fail()
        {
            const string stream = "which_does_not_exist_should_fail";
            using (var connection = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var delete = connection.DeleteStreamAsync(stream, ExpectedVersion.Any);
                Assert.Inconclusive();
                //Assert.That(() => delete.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test]
        public void which_was_allready_deleted_should_fail()
        {
            const string stream = "which_was_allready_deleted_should_fail";
            using (var connection = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = connection.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var delete = connection.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete.Wait);

                var secondDelete = connection.DeleteStreamAsync(stream, ExpectedVersion.Any);
                Assert.That(() => secondDelete.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }
    }
}
