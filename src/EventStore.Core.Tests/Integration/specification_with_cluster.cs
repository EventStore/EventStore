using System;
using System.Net;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Bus;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Core.Tests.Integration {
	public class specification_with_cluster : SpecificationWithDirectoryPerTestFixture {
		protected MiniClusterNode[] _nodes = new MiniClusterNode[3];
		protected Endpoints[] _nodeEndpoints = new Endpoints[3];
		protected IEventStoreConnection _conn;
		protected UserCredentials _admin = DefaultData.AdminCredentials;

		protected Dictionary<int, Func<bool, MiniClusterNode>> _nodeCreationFactory =
			new Dictionary<int, Func<bool, MiniClusterNode>>();

		private readonly List<int> _portsUsed = new List<int>();

		protected class Endpoints {
			public readonly IPEndPoint InternalTcp;
			public readonly IPEndPoint InternalTcpSec;
			public readonly IPEndPoint InternalHttp;
			public readonly IPEndPoint ExternalTcp;
			public readonly IPEndPoint ExternalTcpSec;
			public readonly IPEndPoint ExternalHttp;

			public Endpoints(
				int internalTcp, int internalTcpSec, int internalHttp, int externalTcp,
				int externalTcpSec, int externalHttp) {
				var address = IPAddress.Loopback;
				InternalTcp = new IPEndPoint(address, internalTcp);
				InternalTcpSec = new IPEndPoint(address, internalTcpSec);
				InternalHttp = new IPEndPoint(address, internalHttp);
				ExternalTcp = new IPEndPoint(address, externalTcp);
				ExternalTcpSec = new IPEndPoint(address, externalTcpSec);
				ExternalHttp = new IPEndPoint(address, externalHttp);
			}
		}

		private int GetFreePort(IPAddress ip) {
			var port = PortsHelper.GetAvailablePort(ip);
			_portsUsed.Add(port);
			return port;
		}

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

#if DEBUG
			QueueStatsCollector.InitializeIdleDetection();
#endif
			_nodeEndpoints[0] = new Endpoints(
				GetFreePort(IPAddress.Loopback), GetFreePort(IPAddress.Loopback),
				GetFreePort(IPAddress.Loopback), GetFreePort(IPAddress.Loopback),
				GetFreePort(IPAddress.Loopback), GetFreePort(IPAddress.Loopback));
			_nodeEndpoints[1] = new Endpoints(
				GetFreePort(IPAddress.Loopback), GetFreePort(IPAddress.Loopback),
				GetFreePort(IPAddress.Loopback), GetFreePort(IPAddress.Loopback),
				GetFreePort(IPAddress.Loopback), GetFreePort(IPAddress.Loopback));
			_nodeEndpoints[2] = new Endpoints(
				GetFreePort(IPAddress.Loopback), GetFreePort(IPAddress.Loopback),
				GetFreePort(IPAddress.Loopback), GetFreePort(IPAddress.Loopback),
				GetFreePort(IPAddress.Loopback), GetFreePort(IPAddress.Loopback));

			_nodeCreationFactory.Add(0, (wait) => CreateNode(0,
				_nodeEndpoints[0], new IPEndPoint[] {_nodeEndpoints[1].InternalHttp, _nodeEndpoints[2].InternalHttp},
				wait));
			_nodeCreationFactory.Add(1, (wait) => CreateNode(1,
				_nodeEndpoints[1], new IPEndPoint[] {_nodeEndpoints[0].InternalHttp, _nodeEndpoints[2].InternalHttp},
				wait));
			_nodeCreationFactory.Add(2, (wait) => CreateNode(2,
				_nodeEndpoints[2], new IPEndPoint[] {_nodeEndpoints[0].InternalHttp, _nodeEndpoints[1].InternalHttp},
				wait));

			_nodes[0] = _nodeCreationFactory[0](true);
			_nodes[1] = _nodeCreationFactory[1](true);
			_nodes[2] = _nodeCreationFactory[2](true);

			BeforeNodesStart();

			_nodes[0].Start();
			_nodes[1].Start();
			_nodes[2].Start();

			WaitHandle.WaitAll(new[] {_nodes[0].StartedEvent, _nodes[1].StartedEvent, _nodes[2].StartedEvent});
			QueueStatsCollector.WaitIdle(waitForNonEmptyTf: true);

			_conn = EventStoreConnection.Create(_nodes[0].ExternalTcpEndPoint);
			_conn.ConnectAsync().Wait();

			QueueStatsCollector.WaitIdle();

			Given();
		}

		protected virtual void BeforeNodesStart() {
		}

		protected virtual void Given() {
		}

		protected void ShutdownNode(int nodeNum) {
			_nodes[nodeNum].Shutdown(keepDb: true, keepPorts: true);
		}

		protected void StartNode(int nodeNum) {
			_nodes[nodeNum] = _nodeCreationFactory[nodeNum](false);
			_nodes[nodeNum].Start();
			WaitHandle.WaitAll(new[] {_nodes[nodeNum].StartedEvent});
		}

		private MiniClusterNode CreateNode(int index, Endpoints endpoints, IPEndPoint[] gossipSeeds, bool wait = true) {
			var node = new MiniClusterNode(
				PathName, index, endpoints.InternalTcp, endpoints.InternalTcpSec, endpoints.InternalHttp,
				endpoints.ExternalTcp,
				endpoints.ExternalTcpSec, endpoints.ExternalHttp, skipInitializeStandardUsersCheck: false,
				subsystems: new ISubsystem[] { }, gossipSeeds: gossipSeeds, inMemDb: false);
			if (wait)
				WaitIdle();
			return node;
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			for (var i = 0; i < _portsUsed.Count; i++) {
				PortsHelper.ReturnPort(_portsUsed[i]);
			}

			_conn.Close();
			_nodes[0].Shutdown();
			_nodes[1].Shutdown();
			_nodes[2].Shutdown();
#if DEBUG
			QueueStatsCollector.DisableIdleDetection();
#endif
			base.TestFixtureTearDown();
		}

		protected static void WaitIdle() {
			QueueStatsCollector.WaitIdle();
		}

		protected MiniClusterNode GetMaster() {
			return _nodes.First(x => x.NodeState == Data.VNodeState.Master);
		}

		protected MiniClusterNode[] GetSlaves() {
			return _nodes.Where(x => x.NodeState != Data.VNodeState.Master).ToArray();
		}
	}
}
