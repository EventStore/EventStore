using System;

namespace EventStore.Grpc {
	public class ScavengeNotFoundException : Exception {
		public string ScavengeId { get; }

		public ScavengeNotFoundException(string scavengeId, Exception exception = default) : base(
			$"Scavenge not found. The currently running scavenge is {scavengeId ?? "<unknown>"}.", exception) {
			ScavengeId = scavengeId;
		}
	}
}
