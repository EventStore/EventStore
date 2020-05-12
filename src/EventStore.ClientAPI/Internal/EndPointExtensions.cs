using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace EventStore.ClientAPI.Internal {
	static class EndPointExtensions {
		public static Uri ToESTcpUri(this IPEndPoint ipEndPoint) {	
			return new Uri(string.Format("tcp://{0}:{1}", ipEndPoint.Address, ipEndPoint.Port));	
		}	

		public static Uri ToESTcpUri(this IPEndPoint ipEndPoint, string username, string password) {	
			return new Uri(string.Format("tcp://{0}:{1}@{2}:{3}", username, password, ipEndPoint.Address,	
				ipEndPoint.Port));	
		}
		
		public static string GetHost(this EndPoint endpoint) =>
			endpoint switch {
				IPEndPoint ip => ip.Address.ToString(),
				DnsEndPoint dns => dns.Host,
				_ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint?.GetType(),
					"An invalid endpoint has been provided")
			};
		
		public static int GetPort(this EndPoint endpoint) =>
			endpoint switch {
				IPEndPoint ip => ip.Port,
				DnsEndPoint dns => dns.Port,
				_ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint?.GetType(),
					"An invalid endpoint has been provided")
			};
		
		public static IPEndPoint ResolveDnsToIPAddress(this EndPoint endpoint) {
			var entries = Dns.GetHostAddresses(endpoint.GetHost());
			if (entries.Length == 0)
				throw new Exception($"Unable get host addresses for DNS host ({endpoint.GetHost()})");
			var ipaddress = entries.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
			if (ipaddress == null)
				throw new Exception($"Could not get an IPv4 address for host '{endpoint.GetHost()}'");
			return new IPEndPoint(ipaddress, endpoint.GetPort());
		}
	}
}
