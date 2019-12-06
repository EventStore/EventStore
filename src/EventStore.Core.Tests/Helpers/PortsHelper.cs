using System.Net;
using EventStore.Common.Log;
using System.Net.Sockets;

namespace EventStore.Core.Tests.Helpers {
	public static class PortsHelper {
		private static readonly ILogger Log = LogManager.GetLogger("PortsHelper");
		public static int GetAvailablePort(IPAddress ip) {
			TcpListener l = new TcpListener(ip, 0);
			l.Start();
			int port = ((IPEndPoint)l.LocalEndpoint).Port;
			l.Stop();
			Log.Info($"Available port found: {port}");
			return port;
		}
	}
}
