using EventStore.ClientAPI;
using NUnit.Framework;
using EventStore.Core.Tests.Helpers;
using System;
using System.Linq;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class when_connecting_with_connection_string : SpecificationWithDirectoryPerTestFixture {
		private MiniNode _node;

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
		public void should_not_throw_when_connect_to_is_set() {
			string connectionString = string.Format("ConnectTo=tcp://{0};", _node.TcpEndPoint);
			using (var connection = EventStoreConnection.Create(connectionString)) {
				Assert.DoesNotThrow(connection.ConnectAsync().Wait);
				connection.Close();
			}
		}

		[Test]
		public void should_not_throw_when_only_gossip_seeds_is_set() {
			string connectionString = string.Format("GossipSeeds={0};", _node.IntHttpEndPoint);
			IEventStoreConnection connection = null;

			Assert.DoesNotThrow(() => connection = EventStoreConnection.Create(connectionString));
			Assert.AreEqual(_node.IntHttpEndPoint, connection.Settings.GossipSeeds.First().EndPoint);

			connection.Dispose();
		}

		[Test]
		public void should_throw_when_gossip_seeds_and_connect_to_is_set() {
			string connectionString = string.Format("ConnectTo=tcp://{0};GossipSeeds={1}", _node.TcpEndPoint,
				_node.IntHttpEndPoint);
			Assert.Throws<NotSupportedException>(() => EventStoreConnection.Create(connectionString));
		}

		[Test]
		public void should_throw_when_neither_gossip_seeds_nor_connect_to_is_set() {
			string connectionString = string.Format("HeartBeatTimeout=2000");
			Assert.Throws<Exception>(() => EventStoreConnection.Create(connectionString));
		}
	}
}
