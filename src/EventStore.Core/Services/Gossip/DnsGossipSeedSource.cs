using System;
using System.Linq;
using System.Net;

namespace EventStore.Core.Services.Gossip {
	public class DnsGossipSeedSource : IGossipSeedSource {
		private readonly string _hostname;
		private readonly int _managerHttpPort;

		public DnsGossipSeedSource(string hostname, int managerHttpPort) {
			_hostname = hostname;
			_managerHttpPort = managerHttpPort;
		}

		public IAsyncResult BeginGetHostEndpoints(AsyncCallback requestCallback, object state) {
			return Dns.BeginGetHostAddresses(_hostname, requestCallback, state);
		}

		public IPEndPoint[] EndGetHostEndpoints(IAsyncResult asyncResult) {
			var addresses = Dns.EndGetHostAddresses(asyncResult);

			return addresses.Select(address => new IPEndPoint(address, _managerHttpPort)).ToArray();
		}
	}
}
