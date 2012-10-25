using System;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture]
    internal class creating_stream
    {
        [Test]
        [Category("Network")]
        public void which_does_not_exist_should_be_successfull()
        {
            const string stream = "which_does_not_exist_should_be_successfull";
            using (var connection = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = connection.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);
            }
        }

        [Test]
        [Category("Network")]
        public void which_supposed_to_be_system_should_succees__but_on_your_own_risk()
        {
            const string stream = "$which_supposed_to_be_system_should_succees__but_on_your_own_risk";
            using (var connection = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = connection.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);
            }
        }

        [Test]
        [Category("Network")]
        public void which_already_exists_should_fail()
        {
            const string stream = "which_already_exists_should_fail";
            using (var connection = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var initialCreate = connection.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(initialCreate.Wait);

                var secondCreate = connection.CreateStreamAsync(stream, new byte[0]);
                Assert.Inconclusive();
                //Assert.That(() => secondCreate.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test]
        [Category("Network")]
        public void which_was_deleted_should_fail()
        {
            const string stream = "which_was_deleted_should_fail";
            using (var connection = new EventStoreConnection(MiniNode.Instance.TcpEndPoint))
            {
                var create = connection.CreateStreamAsync(stream, new byte[0]);
                Assert.DoesNotThrow(create.Wait);

                var delete = connection.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete.Wait);

                var secondCreate = connection.CreateStreamAsync(stream, new byte[0]);
                Assert.That(() => secondCreate.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }
    }
}