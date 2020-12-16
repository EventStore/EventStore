namespace EventStore.ClientAPI.Internal {
	public interface IGossipSeed {
		string ToHttpUrl();
		string GetHostHeader();
	}
}
