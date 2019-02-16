using System;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture(TcpType.Normal), TestFixture(TcpType.Ssl), Category("ClientAPI"), Category("LongRunning")]
	public class event_store_connection_should : SpecificationWithDirectoryPerTestFixture {
		private readonly TcpType _tcpType;
		private MiniNode _node;

		public event_store_connection_should(TcpType tcpType) {
			_tcpType = tcpType;
		}

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			_node = new MiniNode(PathName);
			_node.Start();
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			_node.Shutdown();
			base.TestFixtureTearDown();
		}

		[Test]
		[Category("Network")]
		public void not_throw_on_close_if_connect_was_not_called() {
			var connection = TestConnection.To(_node, _tcpType);
			Assert.DoesNotThrow(connection.Close);
		}

		[Test]
		[Category("Network")]
		public void not_throw_on_close_if_called_multiple_times() {
			var connection = TestConnection.To(_node, _tcpType);
			connection.ConnectAsync().Wait();
			connection.Close();
			Assert.DoesNotThrow(connection.Close);
		}

/*
//TODO WEIRD TEST GFY
        [Test]
        [Category("Network")]
        public void throw_on_connect_called_more_than_once()
        {
            var connection = TestConnection.To(_node, _tcpType);
            Assert.DoesNotThrow(() => connection.ConnectAsync().Wait());

            Assert.That(() => connection.ConnectAsync().Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
        }

        [Test]
        [Category("Network")]
        public void throw_on_connect_called_after_close()
        {
            var connection = TestConnection.To(_node, _tcpType);
            connection.ConnectAsync().Wait();
            connection.Close();

            Assert.That(() => connection.ConnectAsync().Wait(),
                        Throws.Exception.InstanceOf<AggregateException>().With.InnerException.InstanceOf<InvalidOperationException>());
        }
*/
		[Test]
		[Category("Network")]
		public void throw_invalid_operation_on_every_api_call_if_connect_was_not_called() {
			var connection = TestConnection.To(_node, _tcpType);

			const string s = "stream";
			var events = new[] {TestEvent.NewTestEvent()};

			Assert.That(() => connection.DeleteStreamAsync(s, 0).Wait(),
				Throws.Exception.InstanceOf<AggregateException>().With.InnerException
					.InstanceOf<InvalidOperationException>());

			Assert.That(() => connection.AppendToStreamAsync(s, 0, events).Wait(),
				Throws.Exception.InstanceOf<AggregateException>().With.InnerException
					.InstanceOf<InvalidOperationException>());

			Assert.That(() => connection.ReadStreamEventsForwardAsync(s, 0, 1, resolveLinkTos: false).Wait(),
				Throws.Exception.InstanceOf<AggregateException>().With.InnerException
					.InstanceOf<InvalidOperationException>());

			Assert.That(() => connection.ReadStreamEventsBackwardAsync(s, 0, 1, resolveLinkTos: false).Wait(),
				Throws.Exception.InstanceOf<AggregateException>().With.InnerException
					.InstanceOf<InvalidOperationException>());

			Assert.That(() => connection.ReadAllEventsForwardAsync(Position.Start, 1, false).Wait(),
				Throws.Exception.InstanceOf<AggregateException>().With.InnerException
					.InstanceOf<InvalidOperationException>());

			Assert.That(() => connection.ReadAllEventsBackwardAsync(Position.End, 1, false).Wait(),
				Throws.Exception.InstanceOf<AggregateException>().With.InnerException
					.InstanceOf<InvalidOperationException>());

			Assert.That(() => connection.StartTransactionAsync(s, 0).Wait(),
				Throws.Exception.InstanceOf<AggregateException>().With.InnerException
					.InstanceOf<InvalidOperationException>());

			Assert.That(
				() => connection.SubscribeToStreamAsync(s, false, (_, __) => Task.CompletedTask, (_, __, ___) => { })
					.Wait(),
				Throws.Exception.InstanceOf<AggregateException>().With.InnerException
					.InstanceOf<InvalidOperationException>());

			Assert.That(
				() => connection.SubscribeToAllAsync(false, (_, __) => Task.CompletedTask, (_, __, ___) => { }).Wait(),
				Throws.Exception.InstanceOf<AggregateException>().With.InnerException
					.InstanceOf<InvalidOperationException>());
		}
	}
}
