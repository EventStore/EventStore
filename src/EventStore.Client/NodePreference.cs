namespace EventStore.Client {
	public enum NodePreference {
		Leader,
		Follower,
		Random,
		ReadOnlyReplica
	}
}
