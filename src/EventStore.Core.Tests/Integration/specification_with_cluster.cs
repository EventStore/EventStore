﻿using System;
using System.Net;
using EventStore.ClientAPI;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace EventStore.Core.Tests.Integration {
	public abstract class specification_with_cluster<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture {
		protected readonly MiniClusterNode<TLogFormat, TStreamId>[] _nodes = new MiniClusterNode<TLogFormat, TStreamId>[3];
		protected readonly Endpoints[] _nodeEndpoints = new Endpoints[3];
		protected IEventStoreConnection _conn;

		private readonly Dictionary<int, Func<bool, MiniClusterNode<TLogFormat, TStreamId>>> _nodeCreationFactory = new();

		protected class Endpoints {
			public readonly IPEndPoint InternalTcp;
			public readonly IPEndPoint ExternalTcp;
			public readonly IPEndPoint HttpEndPoint;

			public IEnumerable<int> Ports() {
				yield return InternalTcp.Port;
				yield return ExternalTcp.Port;
				yield return HttpEndPoint.Port;
			}

			private readonly List<Socket> _sockets;

			public Endpoints() {
				_sockets = new List<Socket>();

				var defaultLoopBack = new IPEndPoint(IPAddress.Loopback, 0);

				var internalTcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				internalTcp.Bind(defaultLoopBack);
				_sockets.Add(internalTcp);

				var externalTcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				externalTcp.Bind(defaultLoopBack);
				_sockets.Add(externalTcp);

				var httpEndPoint = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				httpEndPoint.Bind(defaultLoopBack);
				_sockets.Add(httpEndPoint);

				InternalTcp = CopyEndpoint((IPEndPoint)internalTcp.LocalEndPoint);
				ExternalTcp = CopyEndpoint((IPEndPoint)externalTcp.LocalEndPoint);
				HttpEndPoint = CopyEndpoint((IPEndPoint)httpEndPoint.LocalEndPoint);
			}

			public void DisposeSockets() {
				foreach (var socket in _sockets) {
					socket.Dispose();
				}
			}

			private static IPEndPoint CopyEndpoint(IPEndPoint endpoint) =>
				new(endpoint.Address, endpoint.Port);
		}

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			_nodeEndpoints[0] = new Endpoints();
			_nodeEndpoints[1] = new Endpoints();
			_nodeEndpoints[2] = new Endpoints();

			_nodeEndpoints[0].DisposeSockets();
			_nodeEndpoints[1].DisposeSockets();
			_nodeEndpoints[2].DisposeSockets();

			var duplicates = _nodeEndpoints[0].Ports().Concat(_nodeEndpoints[1].Ports())
				.Concat(_nodeEndpoints[2].Ports())
				.GroupBy(x => x)
				.Where(g => g.Count() > 1)
				.Select(x => x.Key)
				.ToList();

			Assert.IsEmpty(duplicates);

			_nodeCreationFactory.Add(0, wait => CreateNode(0,
				_nodeEndpoints[0], new[] {_nodeEndpoints[1].HttpEndPoint, _nodeEndpoints[2].HttpEndPoint},
				wait));
			_nodeCreationFactory.Add(1, wait => CreateNode(1,
				_nodeEndpoints[1], new[] {_nodeEndpoints[0].HttpEndPoint, _nodeEndpoints[2].HttpEndPoint},
				wait));
			_nodeCreationFactory.Add(2, wait => CreateNode(2,
				_nodeEndpoints[2], new[] {_nodeEndpoints[0].HttpEndPoint, _nodeEndpoints[1].HttpEndPoint},
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

		protected virtual IEventStoreConnection CreateConnection() =>
			EventStoreConnection.Create(_nodes[0].ExternalTcpEndPoint);

		protected virtual void BeforeNodesStart() {
		}

		protected virtual Task Given() => Task.CompletedTask;

		protected Task ShutdownNode(int nodeNum) => _nodes[nodeNum].Shutdown(keepDb: true);

		protected virtual MiniClusterNode<TLogFormat, TStreamId> CreateNode(int index, Endpoints endpoints, EndPoint[] gossipSeeds,
			bool wait = true) => new(
			PathName, index, endpoints.InternalTcp,
			endpoints.ExternalTcp, endpoints.HttpEndPoint,
			subsystems: Array.Empty<ISubsystem>(), gossipSeeds: gossipSeeds, inMemDb: false);

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			_conn.Close();
			await Task.WhenAll(
				_nodes[0].Shutdown(),
				_nodes[1].Shutdown(),
				_nodes[2].Shutdown());

			await base.TestFixtureTearDown();
		}

		protected static void WaitIdle() {
		}

		protected MiniClusterNode<TLogFormat, TStreamId> GetLeader() => _nodes.First(x => x.NodeState == Data.VNodeState.Leader);

		protected MiniClusterNode<TLogFormat, TStreamId>[] GetFollowers() => _nodes.Where(x => x.NodeState == Data.VNodeState.Follower).ToArray();
	}
}
