using System;
using System.Net;

namespace EventStore.Core.Services.Gossip {
	public interface IGossipSeedSource {
		IAsyncResult BeginGetHostEndpoints(AsyncCallback requestCallback, object state);
		EndPoint[] EndGetHostEndpoints(IAsyncResult asyncResult);
	}
}
