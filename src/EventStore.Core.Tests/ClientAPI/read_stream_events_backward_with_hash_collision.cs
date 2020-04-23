using System;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Core.Tests;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.HashCollisions {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class read_stream_events_backward_with_hash_collision : SpecificationWithDirectoryPerTestFixture {
		private MiniNode _node;

		protected virtual IEventStoreConnection BuildConnection(MiniNode node) {
			return TestConnection.To(node, TcpType.Normal);
		}

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			_node = new MiniNode(PathName,
				inMemDb: false,
				memTableSize: 20,
				hashCollisionReadLimit: 1,
				indexBitnessVersion: EventStore.Core.Index.PTableVersions.IndexV1);
			await _node.Start();
		}

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			await _node.Shutdown();
			await base.TestFixtureTearDown();
		}

		[Test]
		public async Task should_return_stream_not_found() {
			const string stream1 = "account--696193173";
			const string stream2 = "LPN-FC002_LPK51001";
			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();
				//Write event to stream 1
				Assert.AreEqual(0,
					(await store.AppendToStreamAsync(stream1, ExpectedVersion.NoStream,
						new EventData(Guid.NewGuid(), "TestEvent", true, null, null))).NextExpectedVersion);
				//Write 100 events to stream 2 which will have the same hash as stream 1.
				for (int i = 0; i < 100; i++) {
					Assert.AreEqual(i,
						(await store.AppendToStreamAsync(stream2, ExpectedVersion.Any,
							new EventData(Guid.NewGuid(), "TestEvent", true, null, null))).NextExpectedVersion);
				}
			}

			var tcpPort = _node.TcpEndPoint.Port;
			var tcpSecPort = _node.TcpSecEndPoint.Port;
			var httpPort = _node.ExtHttpEndPoint.Port;
			await _node.Shutdown(keepDb: true);

			//Restart the node to ensure the read index stream info cache is empty
			_node = new MiniNode(PathName,
				tcpPort, tcpSecPort, httpPort, inMemDb: false,
				memTableSize: 20,
				hashCollisionReadLimit: 1,
				indexBitnessVersion: EventStore.Core.Index.PTableVersions.IndexV1);
			await _node.Start();
			using (var store = BuildConnection(_node)) {
				await store.ConnectAsync();
				Assert.AreEqual(SliceReadStatus.StreamNotFound,
					(await store.ReadStreamEventsBackwardAsync(stream1, 0, 1, true)).Status);
			}
		}
	}
}
