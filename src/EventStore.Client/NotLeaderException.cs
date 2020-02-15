using System;
using System.Net;

namespace EventStore.Client {
	public class NotLeaderException : Exception {
		public IPEndPoint LeaderEndpoint { get; }

		public NotLeaderException(IPEndPoint newLeaderEndpoint, Exception exception = default) : base(
			$"Not leader. New leader at {newLeaderEndpoint}.", exception) {
			LeaderEndpoint = newLeaderEndpoint;
		}
	}
}
