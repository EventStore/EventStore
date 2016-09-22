using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using EventStore.ClientAPI;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using NUnit.Framework;
using EventStore.Core.Tests.ClientAPI;
using EventStore.Core.Tests.Helpers;

namespace EventStore.Core.Tests.Services.Transport.Http
{
    [TestFixture]
    public class when_getting_tcp_stats_from_stat_controller : SpecificationWithMiniNode
    {
        private PortableServer _portableServer;
        private IPEndPoint _serverEndPoint;
        private IEventStoreConnection _connection;
        private string _url;
        
        private List<MonitoringMessage.TcpConnectionStats> _results = new List<MonitoringMessage.TcpConnectionStats>();
        
        protected override void Given()
        {
            _serverEndPoint = new IPEndPoint(IPAddress.Loopback, PortsHelper.GetAvailablePort(IPAddress.Loopback));
            _url = _HttpEndPoint.ToHttpUrl("/stats/tcp");
            
            var settings = ConnectionSettings.Create();
            _connection = EventStoreConnection.Create(settings, _node.TcpEndPoint);
            _connection.ConnectAsync().Wait();
            
            var testEvent = new EventData(Guid.NewGuid(),"TestEvent",true,Encoding.ASCII.GetBytes("{'Test' : 'OneTwoThree'}"),null);
            _connection.AppendToStreamAsync("tests",ExpectedVersion.Any,testEvent).Wait();
            
            _portableServer = new PortableServer(_serverEndPoint);
            _portableServer.SetUp();
        }
        
        protected override void When()
        {
            Func<HttpResponse, bool> verifier = response => {
                _results = Codec.Json.From<List<MonitoringMessage.TcpConnectionStats>>(response.Body);
                return true;
            };
            
            _portableServer.StartServiceAndSendRequest(y => {}, _url, verifier);
        }

        [Test]
        public void should_return_the_external_connections()
        {
            Assert.AreEqual(2, _results.Count(r => r.IsExternalConnection));
        }
        
        [Test]
        public void should_return_the_total_number_of_bytes_sent_for_external_connections()
        {
            Assert.Greater(_results.Sum(r=> r.IsExternalConnection ? r.TotalBytesSent : 0), 0);
        }
        
        [Test]
        public void should_return_the_total_number_of_bytes_received_from_external_connections()
        {
            Assert.Greater(_results.Sum(r => r.IsExternalConnection ? r.TotalBytesReceived : 0), 0);
        }
        
        [OneTimeTearDown]
        public override void TestFixtureTearDown()
        {
            _portableServer.TearDown();
            _connection.Dispose();
            base.TestFixtureTearDown();
        }
    }
}
