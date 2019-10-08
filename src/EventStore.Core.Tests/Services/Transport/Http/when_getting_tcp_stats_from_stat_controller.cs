using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Internal;
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
		private string _url;
		private string _clientConnectionName = "test-connection";

		private MonitoringMessage.TcpConnectionStats _externalConnection;
		private HttpResponse _response;

		protected override void Given() {
			_serverPort = PortsHelper.GetAvailablePort(IPAddress.Loopback);
			_serverEndPoint = new IPEndPoint(IPAddress.Loopback, _serverPort);
			_url = _HttpEndPoint.ToHttpUrl(EndpointExtensions.HTTP_SCHEMA, "/stats/tcp");

			var testEvent = new EventData(Guid.NewGuid(), "TestEvent", true,
				Encoding.ASCII.GetBytes("{'Test' : 'OneTwoThree'}"), null);
			_conn.AppendToStreamAsync("tests", ExpectedVersion.Any, testEvent).Wait();

			_portableServer = new PortableServer(_serverEndPoint);
			_portableServer.SetUp();
		}

		protected override IEventStoreConnection BuildConnection(MiniNode node) {
			var settings = ConnectionSettings.Create()
				.UseCustomLogger(ClientApiLoggerBridge.Default)
				.EnableVerboseLogging()
				.LimitReconnectionsTo(10)
				.SetReconnectionDelayTo(TimeSpan.Zero)
				.SetTimeoutCheckPeriodTo(TimeSpan.FromMilliseconds(100))
				.FailOnNoServerResponse();
			return EventStoreConnection.Create(settings, node.TcpEndPoint.ToESTcpUri(), _clientConnectionName);
		}

		protected override void When() {
			Func<HttpResponse, bool> verifier = response => {
				var results = Codec.Json.From<List<MonitoringMessage.TcpConnectionStats>>(response.Body);
				_externalConnection = results.FirstOrDefault(r =>
					r.IsExternalConnection && r.ClientConnectionName == _clientConnectionName);
				_response = response;
				return true;
			};

			var res = _portableServer.StartServiceAndSendRequest(y => { }, _url, verifier);
			Assert.IsEmpty(res.Item2, "Http call failed");
		}

		[Test]
		public void should_have_succeeded() {
			Assert.AreEqual((int)HttpStatusCode.OK, _response.HttpStatusCode);
		}

		[Test]
		public void should_return_the_named_connection() {
			Assert.NotNull(_externalConnection);
		}

		[Test]
		public void should_return_the_total_number_of_bytes_sent_for_external_connections() {
			Assert.Greater(_externalConnection.TotalBytesSent, 0);
		}

		[Test]
		public void should_return_the_total_number_of_bytes_received_from_external_connections() {
			Assert.Greater(_externalConnection.TotalBytesReceived, 0);
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			PortsHelper.ReturnPort(_serverPort);
			_portableServer.TearDown();
			base.TestFixtureTearDown();
		}
	}
}
