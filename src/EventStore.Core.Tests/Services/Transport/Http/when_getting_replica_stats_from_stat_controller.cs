using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Http
{
	[Category("debug")]
    [TestFixture]
    public class when_getting_replica_stats_from_stat_controller : SpecificationWithDirectoryPerTestFixture
    {
        private readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);
        protected MiniClusterNode[] _nodes = new MiniClusterNode[2];
        protected Endpoints[] _nodeEndpoints = new Endpoints[2];

		private string _replicationStatsUrl;

        private List<ReplicationMessage.ReplicationStats> _response;
        
        
        private void StartNodes()
        {
            _nodeEndpoints[0] = new Endpoints(
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback));
            _nodeEndpoints[1] = new Endpoints(
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback));
            
            _nodes[0] = CreateNode(0,
                _nodeEndpoints[0], new IPEndPoint[] { _nodeEndpoints[1].InternalHttp });
            _nodes[1] = CreateNode(1,
                _nodeEndpoints[1], new IPEndPoint[] { _nodeEndpoints[0].InternalHttp });
        }
        
        [TestFixtureSetUp]
        protected void SetUp()
        {
            StartNodes();
            
            // Find the master
            var replicaSubscribed = new ManualResetEvent(false);
            IPEndPoint masterEndPoint = null;
            
            var replicaSubscribedHandler = new Action<ReplicationMessage.ReplicaSubscribed>(msg => {
                masterEndPoint = msg.MasterEndPoint;
                replicaSubscribed.Set();
            });
            
            _nodes[0].Node.MainBus.Subscribe(
                new AdHocHandler<ReplicationMessage.ReplicaSubscribed>(replicaSubscribedHandler));
            _nodes[1].Node.MainBus.Subscribe(
                new AdHocHandler<ReplicationMessage.ReplicaSubscribed>(replicaSubscribedHandler));

			_nodes[0].Start();
			_nodes[1].Start();
			WaitHandle.WaitAll(new [] {_nodes[0].StartedEvent, _nodes[1].StartedEvent});
            
            if(!replicaSubscribed.WaitOne(Timeout))
            {
                Assert.Fail("Timed out while waiting for replica to subscribe to master.");
            }
            var masterNode = _nodes.First(n => n.InternalTcpEndPoint.ToString() == masterEndPoint.ToString());
            _replicationStatsUrl = masterNode.ExternalHttpEndPoint.ToHttpUrl("/stats/replication");
            
            When();
        }
        
        protected void When() 
        {
            var httpWebRequest = WebRequest.Create(_replicationStatsUrl);
            httpWebRequest.Method = "GET";
            httpWebRequest.UseDefaultCredentials = true;
            
            var response = httpWebRequest.GetResponse();
            
            using (var memoryStream = new MemoryStream())
            {
                response.GetResponseStream().CopyTo(memoryStream);
                var bytes = memoryStream.ToArray();
                var body = Helper.UTF8NoBom.GetString(bytes);
                
                _response = body.ParseJson<List<ReplicationMessage.ReplicationStats>>();
            }
        }

        [Test]
        public void should_return_replica_stats_from_the_master_node()
        {
            Assert.IsNotNull(_response, "No replication stats were returned from master.");
			Assert.AreEqual(1, _response.Count(),
                string.Format("Expected master to have 1 set of replication stats. Got {0}", _response.Count()));
        }
        
        [TestFixtureTearDown]
        protected void TearDown()
        {
            _nodes[0].Shutdown();
            _nodes[1].Shutdown();
        }
        
        private MiniClusterNode CreateNode(int index, Endpoints endpoints, IPEndPoint[] gossipSeeds)
		{
			return new MiniClusterNode(
				PathName, index, endpoints.InternalTcp, endpoints.InternalTcpSec, endpoints.InternalHttp, endpoints.ExternalTcp,
				endpoints.ExternalTcpSec, endpoints.ExternalHttp, skipInitializeStandardUsersCheck: false, gossipSeeds: gossipSeeds, clusterCount: 2);
		}
        
        protected class Endpoints
		{
			public readonly IPEndPoint InternalTcp;
			public readonly IPEndPoint InternalTcpSec;
			public readonly IPEndPoint InternalHttp;
			public readonly IPEndPoint ExternalTcp;
			public readonly IPEndPoint ExternalTcpSec;
			public readonly IPEndPoint ExternalHttp;
			
			public Endpoints(
				int internalTcp, int internalTcpSec, int internalHttp, int externalTcp,
				int externalTcpSec, int externalHttp)
			{
				var testIp = Environment.GetEnvironmentVariable("ES-TESTIP");
				
				var address = string.IsNullOrEmpty(testIp) ? IPAddress.Loopback : IPAddress.Parse(testIp);
				InternalTcp = new IPEndPoint(address, internalTcp);
				InternalTcpSec = new IPEndPoint(address, internalTcpSec);
				InternalHttp = new IPEndPoint(address, internalHttp);
				ExternalTcp = new IPEndPoint(address, externalTcp);
				ExternalTcpSec = new IPEndPoint(address, externalTcpSec);
				ExternalHttp = new IPEndPoint(address, externalHttp);
			}
		}
    }
}