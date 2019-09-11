using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using NUnit.Framework;
using EventStore.Core.Tests.ClientAPI;
using EventStore.Core.Tests.Helpers;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace EventStore.Core.Tests.Services.Transport.Http {
	[TestFixture]
	public class when_getting_tcp_stats_from_stat_controller : SpecificationWithMiniNode {
		private PortableServer _portableServer;
		private IPEndPoint _serverEndPoint;
		private int _serverPort;
		private IEventStoreConnection _connection;
		private string _url;
		private string _clientConnectionName = "test-connection";

		private List<MonitoringMessage.TcpConnectionStats> _results = new List<MonitoringMessage.TcpConnectionStats>();
		private HttpResponse _response;

		protected override async Task Given() {
			_serverPort = PortsHelper.GetAvailablePort(IPAddress.Loopback);
			_serverEndPoint = new IPEndPoint(IPAddress.Loopback, _serverPort);
			_url = _HttpEndPoint.ToHttpUrl(EndpointExtensions.HTTP_SCHEMA, "/stats/tcp");

			var settings = ConnectionSettings.Create();
			_connection = EventStoreConnection.Create(settings, _node.TcpEndPoint, _clientConnectionName);
			await _connection.ConnectAsync();

			var testEvent = new EventData(Guid.NewGuid(), "TestEvent", true,
				Encoding.ASCII.GetBytes("{'Test' : 'OneTwoThree'}"), null);
			await _connection.AppendToStreamAsync("tests", ExpectedVersion.Any, testEvent);

			_portableServer = new PortableServer(_serverEndPoint);
			_portableServer.SetUp();
		}

		protected override Task When() {
			Func<HttpResponse, bool> verifier = response => {
				_results = Codec.Json.From<List<MonitoringMessage.TcpConnectionStats>>(response.Body);
				_response = response;
				return true;
			};

			var res = _portableServer.StartServiceAndSendRequest(y => { }, _url, verifier);
			Assert.IsEmpty(res.Item2, "Http call failed");
			return Task.CompletedTask;
		}

		[Test]
		public void should_have_succeeded() {
			Assert.AreEqual((int)HttpStatusCode.OK, _response.HttpStatusCode);
		}

		[Test]
		public void should_return_the_external_connections() {
			Assert.AreEqual(2, _results.Count(r => r.IsExternalConnection));
		}

		[Test]
		public void should_return_the_total_number_of_bytes_sent_for_external_connections() {
			Assert.Greater(_results.Sum(r => r.IsExternalConnection ? r.TotalBytesSent : 0), 0);
		}

		[Test]
		public void should_return_the_total_number_of_bytes_received_from_external_connections() {
			Assert.Greater(_results.Sum(r => r.IsExternalConnection ? r.TotalBytesReceived : 0), 0);
		}

		[Test]
		public void should_have_set_the_client_connection_name() {
			Assert.IsTrue(_results.Any(x => x.ClientConnectionName == _clientConnectionName));
		}

		[OneTimeTearDown]
		public override Task TestFixtureTearDown() {
			_portableServer.TearDown();
			_connection.Dispose();
			PortsHelper.ReturnPort(_serverEndPoint.Port);
			return base.TestFixtureTearDown();
		}
	}
}
