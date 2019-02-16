using System;
using System.Net;

namespace EventStore.Core.Services.Gossip {
	public class KnownEndpointGossipSeedSource : IGossipSeedSource {
		private readonly IPEndPoint[] _ipEndPoints;

		public KnownEndpointGossipSeedSource(IPEndPoint[] ipEndPoints) {
			if (ipEndPoints == null)
				throw new ArgumentNullException("ipEndPoints");
			_ipEndPoints = ipEndPoints;
		}

		public IAsyncResult BeginGetHostEndpoints(AsyncCallback requestCallback, object state) {
			requestCallback(null);
			return null;
		}

		public IPEndPoint[] EndGetHostEndpoints(IAsyncResult asyncResult) {
			return _ipEndPoints;
		}
	}
}
