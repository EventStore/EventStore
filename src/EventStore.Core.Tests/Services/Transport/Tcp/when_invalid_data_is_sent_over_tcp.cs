using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Tests.ClientAPI;
using EventStore.Core.Tests.Integration;
using EventStore.Transport.Tcp;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Tcp {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_invalid_data_is_sent_over_tcp<TLogFormat, TStreamId> : specification_with_cluster<TLogFormat, TStreamId> {

		[Timeout(5000)]
		[TestCase("InternalTcpEndPoint", false)]
		[TestCase("ExternalTcpEndPoint", false)]
		public void connection_should_be_closed_by_remote_party(string endpointProperty, bool secure) {
			IPEndPoint endpoint = (IPEndPoint)_nodes[0].GetType().GetProperty(endpointProperty).GetValue(_nodes[0], null);
			var connectedEvent = new ManualResetEvent(false);
			var closedEvent = new ManualResetEvent(false);
			ITcpConnection connection;
			if (!secure) {
				connection = TcpConnection.CreateConnectingTcpConnection(
					Guid.NewGuid(),
					endpoint,
					new TcpClientConnector(),
					TimeSpan.FromSeconds(5),
					(conn) => connectedEvent.Set(),
					(conn, error) => {
						Assert.Fail($"Connection failed: {error}");
					},
					false);
			} else {
				connection = TcpConnectionSsl.CreateConnectingConnection(
					Guid.NewGuid(),
					endpoint.GetHost(),
					endpoint,
					delegate { return (true, null); },
					null,
					new TcpClientConnector(),
					TimeSpan.FromSeconds(5),
					(conn) => connectedEvent.Set(),
					(conn, error) => {
						Assert.Fail($"Connection failed: {error}");
					},
					false);
			}

			connection.ConnectionClosed += (conn, error) => {
				closedEvent.Set();
			};

			connectedEvent.WaitOne();
			var data = new List<ArraySegment<byte>> {
				new ArraySegment<byte>(new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10})
			};
			connection.EnqueueSend(data);
			closedEvent.WaitOne();
			connection.Close("intentional close");
		}
	}
}
