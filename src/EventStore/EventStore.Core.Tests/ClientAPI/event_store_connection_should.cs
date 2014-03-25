using System;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture(TcpType.Normal), TestFixture(TcpType.Ssl), Category("LongRunning")]
    public class event_store_connection_should: SpecificationWithDirectoryPerTestFixture
    {
        private readonly TcpType _tcpType;
        private MiniNode _node;

        public event_store_connection_should(TcpType tcpType)
        {
            _tcpType = tcpType;
        }

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

        [Test]
        [Category("Network")]
        public void not_throw_on_close_if_connect_was_not_called()
        {
            var connection = TestConnection.To(_node, _tcpType);
            Assert.DoesNotThrow(connection.Close);
        }

        [Test]
        [Category("Network")]
        public void not_throw_on_close_if_called_multiple_times()
        {
            var connection = TestConnection.To(_node, _tcpType);
            connection.Connect();
            connection.Close();
            Assert.DoesNotThrow(connection.Close);
        }

        [Test]
        [Category("Network")]
        public void throw_on_connect_called_more_than_once()
        {
            var connection = TestConnection.To(_node, _tcpType);
            Assert.DoesNotThrow(() => connection.Connect());

            Assert.That(() => connection.Connect(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
        }

        [Test]
        [Category("Network")]
        public void throw_on_connect_called_after_close()
        {
            var connection = TestConnection.To(_node, _tcpType);
            connection.Connect();
            connection.Close();

            Assert.That(() => connection.Connect(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
        }

        [Test]
        [Category("Network")]
        public void throw_invalid_operation_on_every_api_call_if_connect_was_not_called()
        {
            var connection = TestConnection.To(_node, _tcpType);

            const string s = "stream";
            var events = new[] { TestEvent.NewTestEvent() };

            Assert.That(() => connection.DeleteStream(s, 0),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
            Assert.That(() => connection.DeleteStreamAsync(s, 0).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.AppendToStream(s, 0, events),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
            Assert.That(() => connection.AppendToStreamAsync(s, 0, events).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.ReadStreamEventsForward(s, 0, 1, false),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
            Assert.That(() => connection.ReadStreamEventsForwardAsync(s, 0, 1, resolveLinkTos: false).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.ReadStreamEventsBackward(s, 0, 1, false),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
            Assert.That(() => connection.ReadStreamEventsBackwardAsync(s, 0, 1, resolveLinkTos: false).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.ReadAllEventsForward(Position.Start, 1, false),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
            Assert.That(() => connection.ReadAllEventsForwardAsync(Position.Start, 1, false).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.ReadAllEventsBackward(Position.End, 1, false),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
            Assert.That(() => connection.ReadAllEventsBackwardAsync(Position.End, 1, false).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.StartTransaction(s, 0),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
            Assert.That(() => connection.StartTransactionAsync(s, 0).Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.SubscribeToStream(s, false, (_, __) => { }, (_, __, ___) => { }),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());

            Assert.That(() => connection.SubscribeToAll(false, (_, __) => { }, (_, __, ___) => { }),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
        }
    }
}
