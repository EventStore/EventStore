using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Helpers;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Client;
using EventStore.Transport.Http.Codecs;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Http
{
    [TestFixture]
    public class when_getting_replica_stats_from_stat_controller : SpecificationWithDirectoryPerTestFixture
    {
        private readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
        protected MiniClusterNode[] _nodes = new MiniClusterNode[2];
        protected Endpoints[] _nodeEndpoints = new Endpoints[3];
        protected HttpAsyncClient _client;
        
		private string _replicationUrl = "/stats/replication";
        private string[] _urls = new string[2];
        
        private List<List<ReplicationMessage.ReplicationStats>> _responses;
        
        [TestFixtureSetUp]
        protected void SetUp()
        {
            _client = new HttpAsyncClient();
            
            _nodeEndpoints[0] = new Endpoints(
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback));
            _nodeEndpoints[1] = new Endpoints(
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback));
            _nodeEndpoints[2] = new Endpoints(
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback));
            
            // We only need two nodes running
            _nodes[0] = CreateNode(0,
                _nodeEndpoints[0], new IPEndPoint[] { _nodeEndpoints[1].InternalHttp, _nodeEndpoints[2].InternalHttp });
            _nodes[1] = CreateNode(1,
                _nodeEndpoints[1], new IPEndPoint[] { _nodeEndpoints[0].InternalHttp, _nodeEndpoints[2].InternalHttp });

            _nodes[0].Start();
            _nodes[1].Start();
            
            var nodesStarted = new CountdownEvent(2);
            _nodes[0].Node.MainBus.Subscribe(
                new AdHocHandler<SystemMessage.SystemStart>(m => nodesStarted.Signal()));
            _nodes[1].Node.MainBus.Subscribe(
                new AdHocHandler<SystemMessage.SystemStart>(m => nodesStarted.Signal()));

            if(!nodesStarted.Wait(Timeout))
            {
                Assert.Fail("Timed out while waiting for the nodes to start");
            }
            
            var replicasSubscribed = new CountdownEvent(1);
            _nodes[0].Node.MainBus.Subscribe(
                new AdHocHandler<ReplicationMessage.ReplicaSubscribed>(m => replicasSubscribed.Signal()));
            _nodes[1].Node.MainBus.Subscribe(
                new AdHocHandler<ReplicationMessage.ReplicaSubscribed>(m => replicasSubscribed.Signal()));

            if(!replicasSubscribed.Wait(Timeout)) 
            {
                Assert.Fail("Timed out while waiting for replicas to subscribe to master");
            }
            
            _urls[0] = string.Format("http://{0}{1}", _nodeEndpoints[0].ExternalHttp.ToString(), _replicationUrl);
            _urls[1] = string.Format("http://{0}{1}", _nodeEndpoints[1].ExternalHttp.ToString(), _replicationUrl);

            When();
        }
        
        protected void When() 
        {
            var signal = new CountdownEvent(2);
            _responses = new List<List<ReplicationMessage.ReplicationStats>>();
            
            var handleResponse = new Action<HttpResponse>(response => {
                _responses.Add(Codec.Json.From<List<ReplicationMessage.ReplicationStats>>(response.Body));
                signal.Signal();
            });
            var handleException = new Action<Exception>(exception => {
                Assert.Fail("Exception when getting replica stats, {0}", exception.ToString());
                signal.Signal();
            });
            
			_client.Get(_urls[0], Timeout, handleResponse, handleException);
			_client.Get(_urls[1], Timeout, handleResponse, handleException);
			
            if(!signal.Wait(Timeout)) 
            {
                Assert.Fail("Timed out while waiting for replica stats");
            }
        }
        
        [Test]
        public void should_have_a_response_from_each_running_node() 
        {
            Assert.AreEqual(2, _responses.Count());
        }
        
        [Test]
        public void should_return_empty_list_from_replica_node() 
        {
			Assert.AreEqual(1, _responses.Count(res => res != null && res.Count() == 0));
        }
        
        [Test]
        public void should_return_replica_stats_from_the_master_node()
        {
            var masterStats = _responses.FirstOrDefault(res => res != null && res.Count() > 0);
        		Assert.IsNotNull(masterStats, "No replication stats were returned from master.");
			Assert.AreEqual(1, masterStats.Count(),
                string.Format("Expected master to have 1 set of replication stats. Got {0}", masterStats.Count()));
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
				endpoints.ExternalTcpSec, endpoints.ExternalHttp, skipInitializeStandardUsersCheck: false, gossipSeeds: gossipSeeds);
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