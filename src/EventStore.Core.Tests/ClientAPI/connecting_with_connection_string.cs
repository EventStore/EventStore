using EventStore.ClientAPI;
using NUnit.Framework;
using EventStore.Core.Tests.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class when_connecting_with_connection_string : SpecificationWithDirectoryPerTestFixture {
		private MiniNode _node;

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			_node = new MiniNode(PathName);
			await _node.Start();
		}

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			await _node.Shutdown();
			await base.TestFixtureTearDown();
		}

		[Test]
		public async Task should_not_throw_when_connect_to_is_set() {
			string connectionString = string.Format("ConnectTo=tcp://{0};", _node.TcpEndPoint);
			using (var connection = EventStoreConnection.Create(connectionString)) {
				await connection.ConnectAsync();
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
