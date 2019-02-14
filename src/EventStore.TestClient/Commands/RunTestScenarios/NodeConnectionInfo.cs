using System.Net;

namespace EventStore.TestClient.Commands.RunTestScenarios {
	internal class NodeConnectionInfo {
		public IPAddress IpAddress { get; private set; }
		public int TcpPort { get; private set; }
		public int HttpPort { get; private set; }

		public NodeConnectionInfo(IPAddress ipAddress, int tcpPort, int httPort) {
			IpAddress = ipAddress;
			TcpPort = tcpPort;
			HttpPort = httPort;
		}

		public override string ToString() {
			return string.Format("[{0}:{1}:{2}]", IpAddress, TcpPort, HttpPort);
		}
	}
}
