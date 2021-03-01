namespace EventStore.ClientAPI {
	/// <summary>
	/// Gossip seed abstraction.
	/// </summary>
	public interface IGossipSeed {
		/// <summary>
		/// Format a seed to a URL.
		/// </summary>
		string ToHttpUrl();
		/// <summary>
		/// Get the HTTP Host header value.
		/// </summary>
		string GetHostHeader();
	}
}
