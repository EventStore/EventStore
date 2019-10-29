using System;
using System.Net;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Bus;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace EventStore.Core.Tests.Integration {
	public class specification_with_cluster : SpecificationWithDirectoryPerTestFixture {
		protected MiniClusterNode[] _nodes = new MiniClusterNode[3];
		protected Endpoints[] _nodeEndpoints = new Endpoints[3];
		protected IEventStoreConnection _conn;
		protected UserCredentials _admin = DefaultData.AdminCredentials;

		protected Dictionary<int, Func<bool, MiniClusterNode>> _nodeCreationFactory =
			new Dictionary<int, Func<bool, MiniClusterNode>>();


		protected class Endpoints {
			public readonly IPEndPoint InternalTcp;
			public readonly IPEndPoint InternalTcpSec;
			public readonly IPEndPoint InternalHttp;
			public readonly IPEndPoint ExternalTcp;
			public readonly IPEndPoint ExternalTcpSec;
			public readonly IPEndPoint ExternalHttp;

			public IEnumerable<int> Ports() {
				yield return InternalTcp.Port;
				yield return InternalTcpSec.Port;
				yield return InternalHttp.Port;
				yield return ExternalTcp.Port;
				yield return ExternalTcpSec.Port;
				yield return ExternalHttp.Port;
			}

			public Endpoints() {
				var defaultLoopBack = new IPEndPoint(IPAddress.Loopback, 0);

				using var internalTcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				internalTcp.Bind(defaultLoopBack);

				using var internalTcpSecure =
					new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				internalTcpSecure.Bind(defaultLoopBack);

				using var internalHttp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				internalHttp.Bind(defaultLoopBack);

				using var externalTcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				externalTcp.Bind(defaultLoopBack);

				using var externalTcpSecure =
					new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				externalTcpSecure.Bind(defaultLoopBack);

				using var externalHttp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				externalHttp.Bind(defaultLoopBack);

				InternalTcp = (IPEndPoint)internalTcp.LocalEndPoint;
				InternalTcpSec = (IPEndPoint)internalTcpSecure.LocalEndPoint;
				InternalHttp = (IPEndPoint)internalHttp.LocalEndPoint;
				ExternalTcp = (IPEndPoint)externalTcp.LocalEndPoint;
				ExternalTcpSec = (IPEndPoint)externalTcpSecure.LocalEndPoint;
				ExternalHttp = (IPEndPoint)externalHttp.LocalEndPoint;
			}
		}

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

#if DEBUG
			QueueStatsCollector.InitializeIdleDetection();
#endif
			_nodeEndpoints[0] = new Endpoints();
			_nodeEndpoints[1] = new Endpoints();
			_nodeEndpoints[2] = new Endpoints();

			var duplicates = _nodeEndpoints[0].Ports().Concat(_nodeEndpoints[1].Ports())
				.Concat(_nodeEndpoints[2].Ports())
				.GroupBy(x => x)
				.Where(g => g.Count() > 1)
				.Select(x => x.Key)
				.ToList();

			Assert.IsEmpty(duplicates);

			_nodeCreationFactory.Add(0, wait => CreateNode(0,
				_nodeEndpoints[0], new[] {_nodeEndpoints[1].InternalHttp, _nodeEndpoints[2].InternalHttp},
				wait));
			_nodeCreationFactory.Add(1, wait => CreateNode(1,
				_nodeEndpoints[1], new[] {_nodeEndpoints[0].InternalHttp, _nodeEndpoints[2].InternalHttp},
				wait));
			_nodeCreationFactory.Add(2, wait => CreateNode(2,
				_nodeEndpoints[2], new[] {_nodeEndpoints[0].InternalHttp, _nodeEndpoints[1].InternalHttp},
				wait));

			_nodes[0] = _nodeCreationFactory[0](true);
			_nodes[1] = _nodeCreationFactory[1](true);
			_nodes[2] = _nodeCreationFactory[2](true);

			BeforeNodesStart();

			_nodes[0].Start();
			_nodes[1].Start();
			_nodes[2].Start();

			await Task.WhenAll(_nodes.Select(x => x.Started)).WithTimeout(TimeSpan.FromSeconds(30));

			_conn = CreateConnection();
			await _conn.ConnectAsync();

			await Given();
		}

		protected virtual IEventStoreConnection CreateConnection() {
			return EventStoreConnection.Create(_nodes[0].ExternalTcpEndPoint);
		}

		protected virtual void BeforeNodesStart() {
		}

		protected virtual Task Given() => Task.CompletedTask;

		protected Task ShutdownNode(int nodeNum) {
			return _nodes[nodeNum].Shutdown(keepDb: true);
		}

		protected virtual MiniClusterNode CreateNode(int index, Endpoints endpoints, IPEndPoint[] gossipSeeds,
			bool wait = true) {
			var node = new MiniClusterNode(
				PathName, index, endpoints.InternalTcp, endpoints.InternalTcpSec, endpoints.InternalHttp,
				endpoints.ExternalTcp,
				endpoints.ExternalTcpSec, endpoints.ExternalHttp, skipInitializeStandardUsersCheck: false,
				subsystems: new ISubsystem[] { }, gossipSeeds: gossipSeeds, inMemDb: false);
			return node;
		}

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			_conn.Close();
			await Task.WhenAll(
				_nodes[0].Shutdown(),
				_nodes[1].Shutdown(),
				_nodes[2].Shutdown());

#if DEBUG
			QueueStatsCollector.DisableIdleDetection();
#endif
			await base.TestFixtureTearDown();
		}

		protected static void WaitIdle() {
		}

		protected MiniClusterNode GetMaster() {
			return _nodes.First(x => x.NodeState == Data.VNodeState.Master);
		}

		protected MiniClusterNode[] GetSlaves() {
			return _nodes.Where(x => x.NodeState != Data.VNodeState.Master).ToArray();
		}
	}
}
