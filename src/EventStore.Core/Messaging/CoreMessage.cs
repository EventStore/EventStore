namespace EventStore.Core.Messaging {
	// The name of this enum and its members are used for metrics
	public enum CoreMessage {
		None,
		Authentication,
		Awake,
		Client,
		ClusterClient,
		Election,
		Gossip,
		Grpc,
		Http,
		IODispatcher,
		LeaderDiscovery,
		Monitoring,
		Misc,
		Replication,
		ReplicationTracking,
		Storage,
		Subscription,
		System,
		Tcp,
		Timer,
		UserManagement,
	}
}
