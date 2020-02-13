namespace EventStore.ClientAPI {
	/// <summary>
	/// Indicates which order of preferred nodes for connecting to.
	/// </summary>
	public enum NodePreference {
		/// <summary>
		/// When attempting connnection, prefers leader node.
		/// </summary>
		Leader,

		/// <summary>
		/// When attempting connnection, prefers follower node.
		/// </summary>
		Follower,

		/// <summary>
		/// When attempting connnection, has no node preference.
		/// </summary>
		Random,

		/// <summary>
		/// When attempting connection, prefers read only replicas.
		/// </summary>
		ReadOnlyReplica
	}
}
